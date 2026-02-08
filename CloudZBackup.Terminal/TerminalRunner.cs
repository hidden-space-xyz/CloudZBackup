using CloudZBackup.Application.Orchestrators.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace CloudZBackup.Terminal;

public sealed class TerminalRunner(IBackupOrchestrator useCase, ILogger<TerminalRunner> logger)
{
    private const int BarWidth = 30;
    private const int ProgressRenderIntervalMs = 80;
    private readonly object progressLock = new();
    private long lastProgressTimestamp;

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
            var progress = new Progress<BackupProgress>(RenderProgressBar);

            BackupResult result = await useCase.ExecuteAsync(request, progress, cancellationToken);

            stopwatch.Stop();
            ClearProgressBar();

            PrintResultsSummary(result, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            ClearProgressBar();
            Console.WriteLine();
            PrintWarning("Operation canceled by user.");
            Environment.ExitCode = 130;
        }
        catch (Exception ex)
        {
            ClearProgressBar();
            Console.WriteLine();
            logger.LogError(ex, "Backup failed.");
            PrintError(ex.Message);
            Environment.ExitCode = 1;
        }
    }

    private static void PrintBanner()
    {
        Console.WriteLine();
        WriteColored("   ╔══════════════════════════════════════════╗", ConsoleColor.Cyan);
        WriteColored("   ║                                          ║", ConsoleColor.Cyan);
        WriteColored("   ║", ConsoleColor.Cyan, false);
        WriteColored("        ☁  CloudZ Backup ☁                ", ConsoleColor.White, false);
        WriteColored("║", ConsoleColor.Cyan);
        WriteColored("   ║                                          ║", ConsoleColor.Cyan);
        WriteColored("   ╚══════════════════════════════════════════╝", ConsoleColor.Cyan);
        Console.WriteLine();
    }

    private static void PrintSection(string title)
    {
        WriteColored($"   ── {title} ─────────────────────────────────", ConsoleColor.DarkCyan);
    }

    private static void PrintKeyValue(string key, string value)
    {
        Console.Write("     ");
        WriteColored($"{key,-14}", ConsoleColor.Gray, false);
        WriteColored(value, ConsoleColor.White);
    }

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

    private static void PrintTableRow(string label, int value, ConsoleColor valueColor = ConsoleColor.White)
    {
        Console.Write("     ");
        WriteColored("│ ", ConsoleColor.DarkGray, false);
        WriteColored($"{label,-20}", ConsoleColor.Gray, false);
        WriteColored(" │ ", ConsoleColor.DarkGray, false);
        WriteColored($"{value,6}", valueColor, false);
        WriteColored(" │", ConsoleColor.DarkGray);
    }

    private static void PrintError(string message)
    {
        Console.WriteLine();
        Console.Write("   ");
        WriteColored($"✖ Error: {message}", ConsoleColor.Red);
        Console.WriteLine();
    }

    private static void PrintWarning(string message)
    {
        Console.Write("   ");
        WriteColored($"⚠ {message}", ConsoleColor.Yellow);
        Console.WriteLine();
    }

    private void RenderProgressBar(BackupProgress p)
    {
        lock (progressLock)
        {
            long now = Stopwatch.GetTimestamp();
            if (p.ProcessedItems < p.TotalItems && lastProgressTimestamp != 0)
            {
                double elapsedMs = (now - lastProgressTimestamp) * 1000.0 / Stopwatch.Frequency;
                if (elapsedMs < ProgressRenderIntervalMs)
                    return;
            }

            lastProgressTimestamp = now;

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
                catch { }
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
            catch { }
        }
    }

    private static void ClearProgressBar()
    {
        try
        {
            Console.Write('\r' + new string(' ', Console.WindowWidth - 1) + '\r');
        }
        catch
        {
            Console.WriteLine();
        }
    }

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

    private static string FormatElapsed(TimeSpan ts) => ts.TotalSeconds < 1
        ? $"{ts.TotalMilliseconds:F0} ms"
        : ts.TotalMinutes < 1
            ? $"{ts.TotalSeconds:F1} s"
            : $"{(int)ts.TotalMinutes}m {ts.Seconds:D2}s";

    private static string? GetArgValue(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

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
