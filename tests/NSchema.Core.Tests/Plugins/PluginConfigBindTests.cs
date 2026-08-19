using System.ComponentModel.DataAnnotations;
using NSchema.Configuration.Plugins;

namespace NSchema.Tests.Plugins;

/// <summary>
/// Options binding: a <see cref="PluginSettings"/> binds its attributes onto an options object the way the
/// configuration binder does — snake_case names to properties, dotted keys to nested objects, values through
/// type converters — validating the target's data annotations, with every finding a diagnostic on the result.
/// </summary>
public sealed class PluginConfigBindTests
{
    private enum TransactionMode { Single, ReadCommitted }

    private sealed class PoolOptions
    {
        public int Max { get; set; } = 8;
    }

    private sealed class DatabaseOptions
    {
        [Required]
        public string? ConnectionString { get; set; }
        public int ConnectionTimeout { get; set; } = 30;
        public bool Ssl { get; set; }
        public TransactionMode TransactionMode { get; set; }
        public TimeSpan? CommandTimeout { get; set; }
        public PoolOptions Pool { get; set; } = new();
    }

    private static PluginSettings Config(params (string Key, string? Value)[] attributes) =>
        new(null, attributes.ToDictionary(a => a.Key, a => a.Value, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void Bind_MapsSnakeCaseAttributesToProperties()
    {
        // Arrange
        var config = Config(
            ("connection_string", "Host=localhost"),
            ("connection_timeout", "60"),
            ("ssl", "true"));

        // Act
        var result = config.Get<DatabaseOptions>();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ConnectionString.ShouldBe("Host=localhost");
        result.Value.ConnectionTimeout.ShouldBe(60);
        result.Value.Ssl.ShouldBeTrue();
    }

    [Fact]
    public void Bind_UnsetAttributes_KeepTheirDefaults()
        => Config(("connection_string", "x")).Get<DatabaseOptions>()
            .Value!.ConnectionTimeout.ShouldBe(30);

    [Fact]
    public void Bind_EnumValue_BindsByMemberNameCaseInsensitively()
        => Config(("connection_string", "x"), ("transaction_mode", "readcommitted"))
            .Get<DatabaseOptions>().Value!.TransactionMode.ShouldBe(TransactionMode.ReadCommitted);

    [Fact]
    public void Bind_Value_ConvertsThroughTypeConverters()
        => Config(("connection_string", "x"), ("command_timeout", "00:00:30"))
            .Get<DatabaseOptions>().Value!.CommandTimeout.ShouldBe(TimeSpan.FromSeconds(30));

    [Fact]
    public void Bind_DottedKey_BindsNestedOptions()
        => Config(("connection_string", "x"), ("pool.max", "20"))
            .Get<DatabaseOptions>().Value!.Pool.Max.ShouldBe(20);

    [Fact]
    public void Bind_MissingRequiredAttribute_IsAnError()
        => Config(("ssl", "true")).Get<DatabaseOptions>().Errors
            .ShouldHaveSingleItem().Message.ShouldContain("ConnectionString");

    [Fact]
    public void Bind_UnknownAttribute_IsAnError()
        // A typo'd attribute matches no property.
        => Config(("connection_string", "x"), ("connection_sting", "y")).Get<DatabaseOptions>()
            .IsFailure.ShouldBeTrue();

    [Fact]
    public void Bind_ValueThatDoesNotFit_IsAnError()
        // A non-boolean cannot bind a bool property.
        => Config(("connection_string", "x"), ("ssl", "notabool")).Get<DatabaseOptions>()
            .IsFailure.ShouldBeTrue();

    [Fact]
    public void Bind_Failure_StillCarriesTheBestEffortInstance()
    {
        // Act
        var result = Config(("ssl", "true")).Get<DatabaseOptions>();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value!.Ssl.ShouldBeTrue();
    }
}
