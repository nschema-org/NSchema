using NSchema.Configuration.Ddl;
using NSchema.Diff.Policies;

namespace NSchema.Tests.Configuration.Ddl;

public sealed class DdlProjectConfigReaderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("nschema-ddlcfg-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private async Task<DdlProjectConfig> Read(string sql)
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "config.sql"), sql, TestContext.Current.CancellationToken);
        return await DdlProjectConfigReader.Read(_directory, environment: null, TestContext.Current.CancellationToken);
    }

    private async Task<DdlProjectConfig> ReadEnvironment(string baseSql, string environment, string overlaySql)
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "config.sql"), baseSql, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_directory, $"config.env.{environment}.sql"), overlaySql, TestContext.Current.CancellationToken);
        return await DdlProjectConfigReader.Read(_directory, environment, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Provider_Postgres_MapsConnectionStringAndTimeout()
    {
        var config = await Read("PROVIDER postgres ( connection_string = 'host=db', command_timeout = 30 );");

        config.Provider!.Postgres!.ConnectionString.ShouldBe("host=db");
        config.Provider.Postgres.CommandTimeout.ShouldBe(30);
    }

    [Fact]
    public async Task Provider_Postgres_MapsUsernameAndPassword()
    {
        var config = await Read("PROVIDER postgres ( connection_string = 'host=db', username = 'app', password = 'secret' );");

        config.Provider!.Postgres!.Username.ShouldBe("app");
        config.Provider.Postgres.Password.ShouldBe("secret");
    }

    [Fact]
    public async Task Backend_File_MapsPath()
        => (await Read("BACKEND file ( path = './state.json' );")).State!.File!.Path.ShouldBe("./state.json");

    [Fact]
    public async Task Backend_S3_MapsBucketAndKey()
    {
        var state = (await Read("BACKEND s3 ( bucket = 'my-bucket', key = 'env/state.json' );")).State!;

        state.S3!.Bucket.ShouldBe("my-bucket");
        state.S3.Key.ShouldBe("env/state.json");
    }

    [Fact]
    public async Task Nschema_DestructiveAction_MapsPolicy()
        => (await Read("NSCHEMA ( destructive_action = 'warn' );")).DestructiveActionPolicy.ShouldBe(DestructiveActionPolicy.Warn);

    [Fact]
    public async Task Nschema_ReservedKeys_AreAcceptedAndIgnored()
    {
        // dialect (provider-driven) and transaction_mode are reserved for forward-compat with the core grammar.
        var config = await Read("NSCHEMA ( dialect = 'postgres', transaction_mode = 'single' );");

        config.DestructiveActionPolicy.ShouldBeNull();
    }

    [Fact]
    public async Task AllThreeBlocks_Compose()
    {
        var config = await Read(
            """
            NSCHEMA ( destructive_action = 'allow' );
            PROVIDER postgres ( connection_string = 'host=db' );
            BACKEND file ( path = './state.json' );
            CREATE SCHEMA app;
            """);

        config.Provider!.Postgres!.ConnectionString.ShouldBe("host=db");
        config.State!.File!.Path.ShouldBe("./state.json");
        config.DestructiveActionPolicy.ShouldBe(DestructiveActionPolicy.Allow);
    }

    [Fact]
    public async Task NoConfigBlocks_ReturnsEmpty()
    {
        var config = await Read("CREATE SCHEMA app;");

        config.Provider.ShouldBeNull();
        config.State.ShouldBeNull();
        config.DestructiveActionPolicy.ShouldBeNull();
    }

    [Fact]
    public async Task UnknownBlockType_Throws()
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read("WORKSPACE staging ( region = 'eu' );")))
            .Message.ShouldContain("Unknown configuration block");

    [Fact]
    public async Task UnknownProvider_Throws()
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read("PROVIDER mysql ( connection_string = 'x' );")))
            .Message.ShouldContain("provider");

    [Fact]
    public async Task UnknownAttribute_Throws()
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read("PROVIDER postgres ( hostname = 'x' );")))
            .Message.ShouldContain("Unknown attribute");

    [Fact]
    public async Task UnknownBackendAttribute_Throws()
        // Each section model rejects its own unknowns; the message names the BACKEND section it came from.
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read("BACKEND s3 ( bukket = 'x' );")))
            .Message.ShouldContain("Unknown attribute 'bukket' in a BACKEND s3 block");

    [Fact]
    public async Task DuplicateProvider_Throws()
        => (await Should.ThrowAsync<InvalidOperationException>(() =>
                Read("PROVIDER postgres ( connection_string = 'a' ); PROVIDER postgres ( connection_string = 'b' );")))
            .Message.ShouldContain("More than one");

    [Fact]
    public async Task InvalidDestructiveAction_Throws()
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read("NSCHEMA ( destructive_action = 'nope' );")))
            .Message.ShouldContain("destructive_action");

    // ── Environment overlays ──────────────────────────────────────────────────

    [Fact]
    public async Task Environment_OverlayReplacesBaseSlice()
    {
        // The base uses a file backend; the prod overlay switches it to S3, replacing the slice wholesale.
        var config = await ReadEnvironment(
            "BACKEND file ( path = './state.json' );",
            "prod",
            "BACKEND s3 ( bucket = 'prod-bucket', key = 'state.json' );");

        config.State!.File.ShouldBeNull();
        config.State.S3!.Bucket.ShouldBe("prod-bucket");
    }

    [Fact]
    public async Task Environment_BaseSliceSurvivesWhenOverlayOmitsIt()
    {
        // The overlay only sets the policy; the base provider carries through unchanged.
        var config = await ReadEnvironment(
            "PROVIDER postgres ( connection_string = 'host=base' );",
            "prod",
            "NSCHEMA ( destructive_action = 'error' );");

        config.Provider!.Postgres!.ConnectionString.ShouldBe("host=base");
        config.DestructiveActionPolicy.ShouldBe(DestructiveActionPolicy.Error);
    }

    [Fact]
    public async Task Environment_NotFound_Throws()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "config.sql"), "CREATE SCHEMA app;", TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<InvalidOperationException>(
                () => DdlProjectConfigReader.Read(_directory, "prod", TestContext.Current.CancellationToken).AsTask()))
            .Message.ShouldContain("environment 'prod'");
    }

    [Fact]
    public async Task Environment_Null_IgnoresOverlayFiles()
    {
        // An overlay file is present, but with no environment selected the base config must not read its blocks.
        await File.WriteAllTextAsync(Path.Combine(_directory, "config.env.prod.sql"),
            "BACKEND s3 ( bucket = 'prod-bucket', key = 'state.json' );", TestContext.Current.CancellationToken);

        var config = await Read("CREATE SCHEMA app;");

        config.State.ShouldBeNull();
    }
}
