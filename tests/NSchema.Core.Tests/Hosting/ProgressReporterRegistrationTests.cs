using Microsoft.Extensions.DependencyInjection;
using NSchema.Operations.Progress;

namespace NSchema.Tests.Hosting;

public sealed class ProgressReporterRegistrationTests
{
    private sealed class RecordingProgress : IProgress<OperationProgress>
    {
        public void Report(OperationProgress value) { }
    }

    private static IProgress<OperationProgress> Resolve(Action<NSchemaApplicationBuilder> configure)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        return builder.Build().Services.GetRequiredService<IProgress<OperationProgress>>();
    }

    [Fact]
    public void Default_RegistersTheNoOpReporter() =>
        Resolve(_ => { }).ShouldBeOfType<NullOperationProgress>();

    [Fact]
    public void UseProgressReporter_RegistersTheReporter() =>
        Resolve(b => b.UseProgressReporter<RecordingProgress>()).ShouldBeOfType<RecordingProgress>();

    [Fact]
    public void UseProgressReporter_OverridesAPreviousChoice() =>
        Resolve(b => b
            .UseProgressReporter<NullOperationProgress>()
            .UseProgressReporter<RecordingProgress>())
            .ShouldBeOfType<RecordingProgress>();
}
