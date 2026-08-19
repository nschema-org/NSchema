using NSchema.Deployment;
using NSchema.Operations;
using NSchema.Plan.PlanFile;
using NSchema.Project;
using NSchema.State;

namespace NSchema.Tests;

public sealed class NSchemaApplicationTests
{
    [Fact]
    public void ExposesTheConsumerSurfaces_OffTheApplication()
    {
        // A front-end reaches everything through `app.X` rather than resolving services itself.
        using var app = NSchemaApplication.CreateBuilder().Build();

        app.Operations.ShouldNotBeNull();
        app.Locks.ShouldNotBeNull();
        app.Database.ShouldBeAssignableTo<IDatabaseProvider>();
        app.Project.ShouldBeAssignableTo<IProjectProvider>();
        app.PlanFile.ShouldBeAssignableTo<IPlanFileManager>();
        app.State.ShouldBeAssignableTo<IDatabaseStateManager>();
    }


    [Fact]
    public async Task Application_IsReusableAcrossMultipleOperations()
    {
        // The single-use guard is gone: a front-end drives the full lifecycle through one application instance,
        // so running more than one operation on it must not throw.
        using var app = NSchemaApplication.CreateBuilder().Build();

        var first = await app.Operations.Doctor(new DoctorArguments(), TestContext.Current.CancellationToken);
        var second = await app.Operations.Doctor(new DoctorArguments(), TestContext.Current.CancellationToken);

        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Doctor_OnBareApplication_ResolvesAndReportsNothingConfigured()
    {
        // Behaviour is exposed directly off the application — a front-end resolves no services itself.
        using var app = NSchemaApplication.CreateBuilder().Build();

        var result = await app.Operations.Doctor(new DoctorArguments(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Checks.ShouldContain(d => d.Message.Contains("not configured"));
    }
}
