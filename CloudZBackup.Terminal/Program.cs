using CloudZBackup.Application.UseCases.Options;
using CloudZBackup.Composition;
using CloudZBackup.Terminal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
});

builder.Services.Configure<BackupOptions>(options =>
{
    // Tweak if desired (defaults are already reasonable).
    options.MaxHashConcurrency = Math.Clamp(Environment.ProcessorCount, 2, 16);
    options.MaxFileIoConcurrency = 4;
});

builder.Services.AddInfrastructure();
builder.Services.AddSingleton<TerminalRunner>();

using IHost host = builder.Build();

TerminalRunner runner = host.Services.GetRequiredService<TerminalRunner>();

using CancellationTokenSource cts = new();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await runner.RunAsync(args, cts.Token);
