namespace NSchema.Tests.Helpers;

/// <summary>Fluent helper so end-to-end tests can drop into raw <c>Services</c> registration mid-chain.</summary>
internal static class BuilderTestExtensions
{
    public static NSchemaApplicationBuilder Tap(this NSchemaApplicationBuilder builder, Action<NSchemaApplicationBuilder> configure)
    {
        configure(builder);
        return builder;
    }
}
