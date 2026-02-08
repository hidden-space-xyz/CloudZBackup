namespace CloudZBackup.Tests.Unit.Composition;

using CloudZBackup.Application.Orchestrators.Interfaces;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Composition;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Unit tests that verify the dependency injection container resolves all registered services.
/// </summary>
[TestFixture]
public sealed class DependencyInjectionTests
{
    private ServiceProvider provider = null!;

    /// <summary>
    /// Verifies that all application-layer services are resolvable from the container.
    /// </summary>
    [Test]
    public void AllApplicationServicesAreResolvable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(this.provider.GetService<ISnapshotService>(), Is.Not.Null);
            Assert.That(this.provider.GetService<IPlanService>(), Is.Not.Null);
            Assert.That(this.provider.GetService<IOverwriteDetectionService>(), Is.Not.Null);
            Assert.That(this.provider.GetService<IBackupExecutionService>(), Is.Not.Null);
            Assert.That(this.provider.GetService<IBackupOrchestrator>(), Is.Not.Null);
        });
    }

    /// <summary>
    /// Verifies that all infrastructure-layer services are resolvable from the container.
    /// </summary>
    [Test]
    public void AllInfrastructureServicesAreResolvable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(this.provider.GetService<IFileSystemService>(), Is.Not.Null);
            Assert.That(this.provider.GetService<IHashingService>(), Is.Not.Null);
        });
    }

    /// <summary>
    /// Verifies that the <see cref="IBackupOrchestrator"/> and all its dependencies
    /// can be fully resolved from the container.
    /// </summary>
    [Test]
    public void BackupOrchestratorCanBeFullyResolved()
    {
        var orchestrator = this.provider.GetRequiredService<IBackupOrchestrator>();

        Assert.That(orchestrator, Is.Not.Null);
    }

    /// <summary>
    /// Configures the dependency injection container before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure();
        services.Configure<BackupOptions>(_ => { });
        services.AddLogging();

        this.provider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Verifies that singleton-registered services return the same instance on subsequent resolutions.
    /// </summary>
    [Test]
    public void SingletonsReturnSameInstance()
    {
        var first = this.provider.GetRequiredService<IFileSystemService>();
        var second = this.provider.GetRequiredService<IFileSystemService>();

        Assert.That(first, Is.SameAs(second));
    }

    /// <summary>
    /// Disposes the service provider after each test.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        this.provider.Dispose();
    }
}
