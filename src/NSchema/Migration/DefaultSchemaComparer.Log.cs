using Microsoft.Extensions.Logging;
using NSchema.Schema;

namespace NSchema.Migration;

internal sealed partial class DefaultSchemaComparer
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Beginning schema comparison")]
    private partial void LogBeginningComparison();

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Comparison complete: {ActionCount} actions generated")]
    private partial void LogComparisonComplete(int actionCount);

    [LoggerMessage(EventId = 10, Level = LogLevel.Debug, Message = "Schema '{Schema}' exists in desired state")]
    private partial void LogSchemaExists(string schema);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "Schema '{Schema}' not found in desired state")]
    private partial void LogSchemaNotInDesired(string schema);

    [LoggerMessage(EventId = 12, Level = LogLevel.Information, Message = "Schema '{Schema}' is new")]
    private partial void LogSchemaNew(string schema);

    [LoggerMessage(EventId = 13, Level = LogLevel.Debug, Message = "Schema '{Schema}' is unchanged")]
    private partial void LogSchemaUnchanged(string schema);

    [LoggerMessage(EventId = 14, Level = LogLevel.Information, Message = "Schema '{OldName}' renamed to '{NewName}'")]
    private partial void LogSchemaRenamed(string oldName, string newName);

    [LoggerMessage(EventId = 15, Level = LogLevel.Information, Message = "Schema '{Schema}' comment changed")]
    private partial void LogSchemaCommentChanged(string schema);

    [LoggerMessage(EventId = 20, Level = LogLevel.Debug, Message = "Table '{Schema}.{Table}' exists in desired state")]
    private partial void LogTableExists(string schema, string table);

    [LoggerMessage(EventId = 21, Level = LogLevel.Information, Message = "Table '{Schema}.{Table}' explicitly marked for removal")]
    private partial void LogTableExplicitlyDropped(string schema, string table);

    [LoggerMessage(EventId = 22, Level = LogLevel.Information, Message = "Table '{Schema}.{Table}' not found in desired state")]
    private partial void LogTableNotInDesired(string schema, string table);

    [LoggerMessage(EventId = 23, Level = LogLevel.Information, Message = "Table '{Schema}.{Table}' not in desired state; skipping (partial schema)")]
    private partial void LogTableSkippedPartial(string schema, string table);

    [LoggerMessage(EventId = 24, Level = LogLevel.Information, Message = "Table '{Schema}.{Table}' is new")]
    private partial void LogTableNew(string schema, string table);

    [LoggerMessage(EventId = 25, Level = LogLevel.Debug, Message = "Table '{Schema}.{Table}' is unchanged")]
    private partial void LogTableUnchanged(string schema, string table);

    [LoggerMessage(EventId = 26, Level = LogLevel.Information, Message = "Table '{Schema}.{OldName}' renamed to '{NewName}'")]
    private partial void LogTableRenamed(string schema, string oldName, string newName);

    [LoggerMessage(EventId = 27, Level = LogLevel.Information, Message = "Table '{Schema}.{Table}' comment changed")]
    private partial void LogTableCommentChanged(string schema, string table);

    [LoggerMessage(EventId = 28, Level = LogLevel.Information, Message = "Creating table '{Schema}.{Table}'")]
    private partial void LogTableCreating(string schema, string table);

    [LoggerMessage(EventId = 30, Level = LogLevel.Debug, Message = "Column '{Schema}.{Table}.{Column}' exists in desired state")]
    private partial void LogColumnExists(string schema, string table, string column);

    [LoggerMessage(EventId = 31, Level = LogLevel.Information, Message = "Column '{Schema}.{Table}.{Column}' not found in desired state")]
    private partial void LogColumnNotInDesired(string schema, string table, string column);

    [LoggerMessage(EventId = 32, Level = LogLevel.Information, Message = "Column '{Schema}.{Table}.{Column}' is new")]
    private partial void LogColumnNew(string schema, string table, string column);

    [LoggerMessage(EventId = 33, Level = LogLevel.Debug, Message = "Column '{Schema}.{Table}.{Column}' is unchanged")]
    private partial void LogColumnUnchanged(string schema, string table, string column);

    [LoggerMessage(EventId = 34, Level = LogLevel.Information, Message = "Column '{Schema}.{Table}.{OldName}' renamed to '{NewName}'")]
    private partial void LogColumnRenamed(string schema, string table, string oldName, string newName);

    [LoggerMessage(EventId = 35, Level = LogLevel.Debug, Message = "Column '{Schema}.{Table}.{Column}' type is unchanged ({Type})")]
    private partial void LogColumnTypeUnchanged(string schema, string table, string column, SqlType type);

    [LoggerMessage(EventId = 36, Level = LogLevel.Information, Message = "Column '{Schema}.{Table}.{Column}' type changed: {OldType} -> {NewType}")]
    private partial void LogColumnTypeChanged(string schema, string table, string column, SqlType oldType, SqlType newType);

    [LoggerMessage(EventId = 37, Level = LogLevel.Debug, Message = "Column '{Schema}.{Table}.{Column}' nullability is unchanged ({Nullability})")]
    private partial void LogColumnNullabilityUnchanged(string schema, string table, string column, string nullability);

    [LoggerMessage(EventId = 38, Level = LogLevel.Information, Message = "Column '{Schema}.{Table}.{Column}' nullability changed: {OldValue} -> {NewValue}")]
    private partial void LogColumnNullabilityChanged(string schema, string table, string column, bool oldValue, bool newValue);

    [LoggerMessage(EventId = 39, Level = LogLevel.Debug, Message = "Column '{Schema}.{Table}.{Column}' default is unchanged ({Default})")]
    private partial void LogColumnDefaultUnchanged(string schema, string table, string column, string @default);

    [LoggerMessage(EventId = 40, Level = LogLevel.Information, Message = "Column '{Schema}.{Table}.{Column}' default changed: '{OldDefault}' -> '{NewDefault}'")]
    private partial void LogColumnDefaultChanged(string schema, string table, string column, string? oldDefault, string? newDefault);

    [LoggerMessage(EventId = 41, Level = LogLevel.Information, Message = "Column '{Schema}.{Table}.{Column}' comment changed")]
    private partial void LogColumnCommentChanged(string schema, string table, string column);

    [LoggerMessage(EventId = 42, Level = LogLevel.Information, Message = "Column '{Schema}.{Table}.{Column}' identity sequence options changed: start {OldStart} -> {NewStart}, min {OldMin} -> {NewMin}, increment {OldIncrement} -> {NewIncrement}")]
    private partial void LogColumnIdentityChanged(string schema, string table, string column, long? oldStart, long? newStart, long? oldMin, long? newMin, long? oldIncrement, long? newIncrement);

    [LoggerMessage(EventId = 50, Level = LogLevel.Debug, Message = "Primary key for '{Schema}.{Table}' is unchanged")]
    private partial void LogPrimaryKeyUnchanged(string schema, string table);

    [LoggerMessage(EventId = 51, Level = LogLevel.Information, Message = "Dropping primary key '{KeyName}' from '{Schema}.{Table}'")]
    private partial void LogPrimaryKeyDropping(string keyName, string schema, string table);

    [LoggerMessage(EventId = 52, Level = LogLevel.Information, Message = "Adding primary key '{KeyName}' to '{Schema}.{Table}'")]
    private partial void LogPrimaryKeyAdding(string keyName, string schema, string table);

    [LoggerMessage(EventId = 60, Level = LogLevel.Information, Message = "Foreign key '{FkName}' on '{Schema}.{Table}' is missing or changed")]
    private partial void LogForeignKeyMissingOrChanged(string fkName, string schema, string table);

    [LoggerMessage(EventId = 61, Level = LogLevel.Debug, Message = "Foreign key '{FkName}' on '{Schema}.{Table}' is unchanged")]
    private partial void LogForeignKeyUnchanged(string fkName, string schema, string table);

    [LoggerMessage(EventId = 62, Level = LogLevel.Information, Message = "Foreign key '{FkName}' on '{Schema}.{Table}' is new or changed")]
    private partial void LogForeignKeyNewOrChanged(string fkName, string schema, string table);

    [LoggerMessage(EventId = 63, Level = LogLevel.Information, Message = "Adding foreign key '{FkName}' to new table '{Schema}.{Table}'")]
    private partial void LogForeignKeyAddingToNewTable(string fkName, string schema, string table);

    [LoggerMessage(EventId = 70, Level = LogLevel.Information, Message = "Index '{IndexName}' on '{Schema}.{Table}' is missing or changed")]
    private partial void LogIndexMissingOrChanged(string indexName, string schema, string table);

    [LoggerMessage(EventId = 71, Level = LogLevel.Information, Message = "Index '{IndexName}' on '{Schema}.{Table}' comment changed")]
    private partial void LogIndexCommentChanged(string indexName, string schema, string table);

    [LoggerMessage(EventId = 72, Level = LogLevel.Debug, Message = "Index '{IndexName}' on '{Schema}.{Table}' is unchanged")]
    private partial void LogIndexUnchanged(string indexName, string schema, string table);

    [LoggerMessage(EventId = 73, Level = LogLevel.Information, Message = "Index '{IndexName}' on '{Schema}.{Table}' is new or changed")]
    private partial void LogIndexNewOrChanged(string indexName, string schema, string table);

    [LoggerMessage(EventId = 74, Level = LogLevel.Information, Message = "Adding index '{IndexName}' to new table '{Schema}.{Table}'")]
    private partial void LogIndexAddingToNewTable(string indexName, string schema, string table);

    [LoggerMessage(EventId = 80, Level = LogLevel.Information, Message = "Revoking USAGE on schema '{Schema}' from '{Role}'")]
    private partial void LogSchemaUsageRevoking(string schema, string role);

    [LoggerMessage(EventId = 81, Level = LogLevel.Information, Message = "Granting USAGE on schema '{Schema}' to '{Role}'")]
    private partial void LogSchemaUsageGranting(string schema, string role);

    [LoggerMessage(EventId = 82, Level = LogLevel.Information, Message = "Revoking all privileges on '{Schema}.{Table}' from '{Role}'")]
    private partial void LogTablePrivilegesRevoking(string schema, string table, string role);

    [LoggerMessage(EventId = 83, Level = LogLevel.Information, Message = "Updating privileges on '{Schema}.{Table}' for '{Role}'")]
    private partial void LogTablePrivilegesUpdating(string schema, string table, string role);

    [LoggerMessage(EventId = 84, Level = LogLevel.Information, Message = "Granting privileges on '{Schema}.{Table}' to '{Role}'")]
    private partial void LogTablePrivilegesGranting(string schema, string table, string role);

    [LoggerMessage(EventId = 85, Level = LogLevel.Information, Message = "Granting privileges on new table '{Schema}.{Table}' to '{Role}'")]
    private partial void LogTablePrivilegesGrantingToNewTable(string schema, string table, string role);
}
