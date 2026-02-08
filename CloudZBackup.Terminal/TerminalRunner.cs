using CloudZBackup.Application.Orchestrators.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace CloudZBackup.Terminal;

/// <summary>
/// Provides the interactive terminal user interface for CloudZBackup,
/// handling argument parsing, progress rendering, and result display.
/// </summary>
public sealed class TerminalRunner(IBackupOrchestrator useCase, ILogger<TerminalRunner> logger)
{
    private const int BarWidth = 30;
    private const int ProgressRenderIntervalMs = 80;
    private readonly object _progressLock = new();
    private long _lastProgressTimestamp;
    private volatile bool _progressEnabled;

    /// <summary>
    /// Entry point that parses command-line arguments (or prompts interactively),
    /// executes the backup, and renders progress and results to the console.
    /// </summary>
    /// <param name="args">Command-line arguments (<c>--source</c>, <c>--dest</c>, <c>--mode</c>).</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    public async Task RunAsync(string[] args, CancellationToken cancellationToken)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        PrintBanner();

        try
        {
            string source = GetArgValue(args, "--source") ?? Prompt("Source path");
            string dest = GetArgValue(args, "--dest") ?? Prompt("Destination path");
            string modeText = GetArgValue(args, "--mode") ?? Prompt("Mode (sync | add | remove)");

            if (!TryParseMode(modeText, out BackupMode mode))
            {
                PrintError("Invalid mode. Allowed values: sync, add, remove.");
                Environment.ExitCode = 2;
                return;
            }

            PrintSection("Configuration");
            PrintKeyValue("Source", source);
            PrintKeyValue("Destination", dest);
            PrintKeyValue("Mode", mode.ToString());
            Console.WriteLine();

            BackupRequest request = new(source, dest, mode);

            logger.LogInformation("Starting backup. Mode: {Mode}", mode);

            PrintSection("Progress");
            var stopwatch = Stopwatch.StartNew();

            _progressEnabled = true;
            var progress = new Progress<BackupProgress>(RenderProgressBar);

            BackupResult result = await useCase.ExecuteAsync(request, progress, cancellationToken);

            stopwatch.Stop();

            lock (_progressLock)
            {
                _progressEnabled = false;
                ClearProgressBar();
                PrintResultsSummary(result, stopwatch.Elapsed);
            }
        }
        catch (OperationCanceledException)
        {
            lock (_progressLock)
            {
                _progressEnabled = false;
                ClearProgressBar();
                Console.WriteLine();
                PrintWarning("Operation canceled by user.");
            }
            Environment.ExitCode = 130;
        }
        catch (Exception ex)
        {
            lock (_progressLock)
            {
                _progressEnabled = false;
                ClearProgressBar();
                Console.WriteLine();
                PrintError(ex.Message);
            }
            logger.LogError(ex, "Backup failed.");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// Prints the application banner to the console.
    /// </summary>
    private static void PrintBanner()
    {
        Console.WriteLine();
        WriteColored("   ╔══════════════════════════════════════════╗", ConsoleColor.Cyan);
        WriteColored("   ║                                          ║", ConsoleColor.Cyan);
        WriteColored("   ║", ConsoleColor.Cyan, false);
        WriteColored("            💾 CloudZBackup 💾            ", ConsoleColor.White, false);
        WriteColored("║", ConsoleColor.Cyan);
        WriteColored("   ║                                          ║", ConsoleColor.Cyan);
        WriteColored("   ╚══════════════════════════════════════════╝", ConsoleColor.Cyan);
        Console.WriteLine();
    }

    /// <summary>
    /// Prints a section header line to the console.
    /// </summary>
    private static void PrintSection(string title)
    {
        WriteColored($"   ── {title} ─────────────────────────────────", ConsoleColor.DarkCyan);
    }

    /// <summary>
    /// Prints a key-value pair to the console.
    /// </summary>
    private static void PrintKeyValue(string key, string value)
    {
        Console.Write("     ");
        WriteColored($"{key,-14}", ConsoleColor.Gray, false);
        WriteColored(value, ConsoleColor.White);
    }

    /// <summary>
    /// Prints the final summary table showing operation counts and elapsed time.
    /// </summary>
    private static void PrintResultsSummary(BackupResult result, TimeSpan elapsed)
    {
        WriteColored("   ✔  Backup completed successfully!", ConsoleColor.Green);
        Console.WriteLine();
        PrintSection("Summary");

        int total = result.DirectoriesCreated
                  + result.FilesCopied
                  + result.FilesOverwritten
                  + result.FilesDeleted
                  + result.DirectoriesDeleted;

        Console.Write("     ");
        WriteColored("┌──────────────────────┬────────┐", ConsoleColor.DarkGray);
        PrintTableRow("Directories created", result.DirectoriesCreated);
        PrintTableRow("Files copied", result.FilesCopied);
        PrintTableRow("Files overwritten", result.FilesOverwritten);
        PrintTableRow("Files deleted", result.FilesDeleted);
        PrintTableRow("Directories deleted", result.DirectoriesDeleted);
        Console.Write("     ");
        WriteColored("├──────────────────────┼────────┤", ConsoleColor.DarkGray);
        PrintTableRow("Total operations", total, ConsoleColor.Cyan);
        Console.Write("     ");
        WriteColored("└──────────────────────┴────────┘", ConsoleColor.DarkGray);

        Console.WriteLine();
        Console.Write("     ");
        WriteColored("Elapsed: ", ConsoleColor.Gray, false);
        WriteColored(FormatElapsed(elapsed), ConsoleColor.White);
        Console.WriteLine();
    }

    /// <summary>
    /// Prints a single row of the results summary table.
    /// </summary>
    private static void PrintTableRow(string label, int value, ConsoleColor valueColor = ConsoleColor.White)
    {
        Console.Write("     ");
        WriteColored("│ ", ConsoleColor.DarkGray, false);
        WriteColored($"{label,-20}", ConsoleColor.Gray, false);
        WriteColored(" │ ", ConsoleColor.DarkGray, false);
        WriteColored($"{value,6}", valueColor, false);
        WriteColored(" │", ConsoleColor.DarkGray);
    }

    /// <summary>
    /// Prints an error message in red to the console.
    /// </summary>
    private static void PrintError(string message)
    {
        Console.WriteLine();
        Console.Write("   ");
        WriteColored($"✖ Error: {message}", ConsoleColor.Red);
        Console.WriteLine();
    }

    /// <summary>
    /// Prints a warning message in yellow to the console.
    /// </summary>
    private static void PrintWarning(string message)
    {
        Console.Write("   ");
        WriteColored($"⚠ {message}", ConsoleColor.Yellow);
        Console.WriteLine();
    }

    /// <summary>
    /// Renders an inline progress bar on the current console line, throttled to avoid excessive redraws.
    /// </summary>
    private void RenderProgressBar(BackupProgress p)
    {
        lock (_progressLock)
        {
            if (!_progressEnabled)
                return;

            long now = Stopwatch.GetTimestamp();
            if (p.ProcessedItems < p.TotalItems && _lastProgressTimestamp != 0)
            {
                double elapsedMs = (now - _lastProgressTimestamp) * 1000.0 / Stopwatch.Frequency;
                if (elapsedMs < ProgressRenderIntervalMs)
                    return;
            }

            _lastProgressTimestamp = now;

            if (p.TotalItems <= 0)
            {
                Console.Write("\r     ");
                WriteColored($"… {p.Phase}", ConsoleColor.DarkYellow, false);
                try
                {
                    int written = 6 + $"… {p.Phase}".Length;
                    if (written < Console.WindowWidth)
                        Console.Write(new string(' ', Console.WindowWidth - written - 1));
                }
                catch
                {
                    // Ignore
                }
                return;
            }

            double ratio = (double)p.ProcessedItems / p.TotalItems;
            int filled = (int)(ratio * BarWidth);
            int empty = BarWidth - filled;
            int percent = (int)(ratio * 100);

            ConsoleColor barColor = percent switch
            {
                < 33 => ConsoleColor.Red,
                < 66 => ConsoleColor.Yellow,
                _ => ConsoleColor.Green,
            };

            Console.Write("\r     ");
            WriteColored("[", ConsoleColor.DarkGray, false);
            WriteColored(new string('█', filled), barColor, false);
            WriteColored(new string('░', empty), ConsoleColor.DarkGray, false);
            WriteColored("]", ConsoleColor.DarkGray, false);
            WriteColored($" {percent,3}%", ConsoleColor.White, false);
            WriteColored($" ({p.ProcessedItems}/{p.TotalItems})", ConsoleColor.Gray, false);
            WriteColored($" {p.Phase}", ConsoleColor.DarkYellow, false);

            try
            {
                int written = 7 + BarWidth + 6 + $" ({p.ProcessedItems}/{p.TotalItems})".Length + $" {p.Phase}".Length;
                if (written < Console.WindowWidth)
                    Console.Write(new string(' ', Console.WindowWidth - written - 1));
            }
            catch
            {
                // Ignore
            }
        }
    }

    /// <summary>
    /// Clears the current progress bar line from the console.
    /// </summary>
    private static void ClearProgressBar()
    {
        try
        {
            int width = Console.BufferWidth;
            Console.Write('\r');
            Console.Write(new string(' ', Math.Max(0, width - 1)));
            Console.Write('\r');
            Console.WriteLine();
        }
        catch
        {
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Writes text to the console in the specified foreground color.
    /// </summary>
    private static void WriteColored(string text, ConsoleColor color, bool newLine = true)
    {
        ConsoleColor previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        if (newLine)
            Console.WriteLine(text);
        else
            Console.Write(text);
        Console.ForegroundColor = previous;
    }

    /// <summary>
    /// Formats a <see cref="TimeSpan"/> into a human-readable short string.
    /// </summary>
    private static string FormatElapsed(TimeSpan ts) => ts.TotalSeconds < 1
        ? $"{ts.TotalMilliseconds:F0} ms"
        : ts.TotalMinutes < 1
            ? $"{ts.TotalSeconds:F1} s"
            : $"{(int)ts.TotalMinutes}m {ts.Seconds:D2}s";

    /// <summary>
    /// Retrieves the value associated with a command-line argument key, or <see langword="null"/> if not found.
    /// </summary>
    private static string? GetArgValue(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    /// <summary>
    /// Prompts the user for input with the specified label and returns the trimmed response.
    /// </summary>
    private static string Prompt(string label)
    {
        Console.Write("   ");
        WriteColored("❓ ", ConsoleColor.Cyan, false);
        WriteColored($"{label}: ", ConsoleColor.White, false);
        ConsoleColor prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        string value = (Console.ReadLine() ?? string.Empty).Trim();
        Console.ForegroundColor = prev;
        return value;
    }

    /// <summary>
    /// Attempts to parse a string into a <see cref="BackupMode"/> value.
    /// </summary>
    private static bool TryParseMode(string input, out BackupMode mode)
    {
        switch ((input ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "sync":
                mode = BackupMode.Sync;
                return true;
            case "add":
                mode = BackupMode.Add;
                return true;
            case "remove":
                mode = BackupMode.Remove;
                return true;
            default:
                mode = default;
                return false;
        }
    }
}
