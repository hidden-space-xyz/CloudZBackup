using CloudZBackup.Application.UseCases.Options;
using CloudZBackup.Composition;
using CloudZBackup.Terminal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

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

using var host = builder.Build();

var runner = host.Services.GetRequiredService<TerminalRunner>();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await runner.RunAsync(args, cts.Token);
