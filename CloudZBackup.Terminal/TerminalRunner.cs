using CloudZBackup.Application.Orchestrators.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CloudZBackup.Terminal;

public sealed class TerminalRunner(IBackupOrchestrator useCase, ILogger<TerminalRunner> logger)
{
    public async Task RunAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            // Interactive by default; also supports optional args:
            // --source "C:\A" --dest "D:\B" --mode sync
            string source = GetArgValue(args, "--source") ?? Prompt("Source path");
            string dest = GetArgValue(args, "--dest") ?? Prompt("Destination path");
            string modeText = GetArgValue(args, "--mode") ?? Prompt("Mode (sync | add | remove)");

            if (!TryParseMode(modeText, out BackupMode mode))
            {
                await Console.Error.WriteLineAsync(
                    "Invalid mode. Allowed values: sync, add, remove."
                );
                Environment.ExitCode = 2;
                return;
            }

            BackupRequest request = new(source, dest, mode);

            logger.LogInformation("Starting backup. Mode: {Mode}", mode);

            BackupResult result = await useCase.ExecuteAsync(request, cancellationToken);

            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("Backup completed successfully.");
            await Console.Out.WriteLineAsync($"Directories created : {result.DirectoriesCreated}");
            await Console.Out.WriteLineAsync($"Files copied        : {result.FilesCopied}");
            await Console.Out.WriteLineAsync($"Files overwritten   : {result.FilesOverwritten}");
            await Console.Out.WriteLineAsync($"Files deleted       : {result.FilesDeleted}");
            await Console.Out.WriteLineAsync($"Directories deleted : {result.DirectoriesDeleted}");
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("Operation canceled.");
            Environment.ExitCode = 130;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backup failed.");
            await Console.Error.WriteLineAsync(ex.Message);
            Environment.ExitCode = 1;
        }
    }

    private static string Prompt(string label)
    {
        Console.Write($"{label}: ");
        return (Console.ReadLine() ?? string.Empty).Trim();
    }

    private static string? GetArgValue(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
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
