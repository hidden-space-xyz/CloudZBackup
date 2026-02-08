using CloudZBackup.Application.Orchestrators.Interfaces;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace CloudZBackup.Tests.Unit.Composition;

[TestFixture]
public sealed class DependencyInjectionTests
{
    private ServiceProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure();
        services.Configure<BackupOptions>(_ => { });
        services.AddLogging();

        _provider = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _provider.Dispose();
    }

    [Test]
    public void AllApplicationServices_AreResolvable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_provider.GetService<ISnapshotService>(), Is.Not.Null);
            Assert.That(_provider.GetService<IPlanService>(), Is.Not.Null);
            Assert.That(_provider.GetService<IOverwriteDetectionService>(), Is.Not.Null);
            Assert.That(_provider.GetService<IBackupExecutionService>(), Is.Not.Null);
            Assert.That(_provider.GetService<IBackupOrchestrator>(), Is.Not.Null);
        });
    }

    [Test]
    public void AllInfrastructureServices_AreResolvable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_provider.GetService<IFileSystemService>(), Is.Not.Null);
            Assert.That(_provider.GetService<IHashingService>(), Is.Not.Null);
        });
    }

    [Test]
    public void BackupOrchestrator_CanBeFullyResolved()
    {
        var orchestrator = _provider.GetRequiredService<IBackupOrchestrator>();

        Assert.That(orchestrator, Is.Not.Null);
    }

    [Test]
    public void Singletons_ReturnSameInstance()
    {
        var first = _provider.GetRequiredService<IFileSystemService>();
        var second = _provider.GetRequiredService<IFileSystemService>();

        Assert.That(first, Is.SameAs(second));
    }
}
