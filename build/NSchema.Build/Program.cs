using Hamelin;
using Hamelin.Runtimes.GitHubActions;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Build.Models;
using NSchema.Build.Services;
using NSchema.Build.Steps;
using Version = NSchema.Build.Steps.Version;

var builder = PipelineApplication.CreateBuilder(args);

builder.Services
    .AddScoped<ICommandRunner, CliWrapCommandRunner>()
    .AddGitHubActionsRuntime()
    .AddStepsFromAssemblyContaining<Program>();

builder.Services.AddOptions<BuildOptions>()
    .BindConfiguration("Build")
    .Validate(b => !string.IsNullOrEmpty(b.ArtifactsDirectory))
    .Validate(b => !string.IsNullOrEmpty(b.TempDirectory))
    .Validate(b => !string.IsNullOrEmpty(b.Configuration))
    .Validate(b => !string.IsNullOrEmpty(b.ProjectFile))
    .Validate(b => !string.IsNullOrEmpty(b.ChangelogFile))
    .ValidateOnStart();

var pipeline = builder.Build();
return args switch
{
    ["build"] => pipeline
        .UseStep<Clean>()
        .UseStep<Format>()
        .UseStep<ExtractProject>()
        .UseStep<Version>()
        .UseStep<Changelog>()
        .UseStep<Restore>()
        .UseStep<Build>()
        .UseStep<Test>()
        .RunWithExitCode(),
    ["deploy"] => pipeline
        .UseStep<Clean>()
        .UseStep<ExtractProject>()
        .UseStep<Version>()
        .UseStep<Changelog>()
        .UseStep<Restore>()
        .UseStep<Build>()
        .UseStep<Test>()
        .UseStep<Pack>()
        .UseStep<CreateTag>()
        .UseStep<CreateRelease>()
        .UseStep<Publish>()
        .RunWithExitCode(),
    _ => Help()
};

static int Help()
{
    Console.Error.WriteLine("Usage: nschema-build <build|deploy>");
    return 1;
}
