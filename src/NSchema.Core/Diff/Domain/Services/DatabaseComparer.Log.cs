using Microsoft.Extensions.Logging;
using NSchema.Model;
using NSchema.Model.Columns;

namespace NSchema.Diff.Domain.Services;

internal sealed partial class DatabaseComparer
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Beginning schema comparison")]
    private partial void LogBeginningComparison();

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Comparison complete: {ActionCount} actions generated")]
    private partial void LogComparisonComplete(int actionCount);

    [LoggerMessage(EventId = 10, Level = LogLevel.Debug, Message = "Schema '{Schema}' exists in desired state")]
    private partial void LogSchemaExists(SqlIdentifier schema);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "Schema '{Schema}' not found in desired state")]
    private partial void LogSchemaNotInDesired(SqlIdentifier schema);

    [LoggerMessage(EventId = 12, Level = LogLevel.Information, Message = "Schema '{Schema}' is new")]
    private partial void LogSchemaNew(SqlIdentifier schema);

    [LoggerMessage(EventId = 13, Level = LogLevel.Debug, Message = "Schema '{Schema}' is unchanged")]
    private partial void LogSchemaUnchanged(SqlIdentifier schema);

    [LoggerMessage(EventId = 14, Level = LogLevel.Information, Message = "Schema '{OldName}' renamed to '{NewName}'")]
    private partial void LogSchemaRenamed(SqlIdentifier oldName, SqlIdentifier newName);

    [LoggerMessage(EventId = 15, Level = LogLevel.Information, Message = "Schema '{Schema}' comment changed")]
    private partial void LogSchemaCommentChanged(SqlIdentifier schema);

    [LoggerMessage(EventId = 20, Level = LogLevel.Debug, Message = "Table '{Schema}.{Table}' exists in desired state")]
    private partial void LogTableExists(SqlIdentifier schema, SqlIdentifier table);

    [LoggerMessage(EventId = 22, Level = LogLevel.Information, Message = "Table '{Schema}.{Table}' not found in desired state")]
    private partial void LogTableNotInDesired(SqlIdentifier schema, SqlIdentifier table);

    [LoggerMessage(EventId = 24, Level = LogLevel.Information, Message = "Table '{Schema}.{Table}' is new")]
    private partial void LogTableNew(SqlIdentifier schema, SqlIdentifier table);

    [LoggerMessage(EventId = 25, Level = LogLevel.Debug, Message = "Table '{Schema}.{Table}' is unchanged")]
    private partial void LogTableUnchanged(SqlIdentifier schema, SqlIdentifier table);

    [LoggerMessage(EventId = 26, Level = LogLevel.Information, Message = "Table '{Schema}.{OldName}' renamed to '{NewName}'")]
    private partial void LogTableRenamed(SqlIdentifier schema, SqlIdentifier oldName, SqlIdentifier newName);

    [LoggerMessage(EventId = 27, Level = LogLevel.Information, Message = "Table '{Schema}.{Table}' comment changed")]
    private partial void LogTableCommentChanged(SqlIdentifier schema, SqlIdentifier table);

    [LoggerMessage(EventId = 28, Level = LogLevel.Information, Message = "Creating table '{Schema}.{Table}'")]
    private partial void LogTableCreating(SqlIdentifier schema, SqlIdentifier table);

    [LoggerMessage(EventId = 30, Level = LogLevel.Debug, Message = "Column '{Owner}.{Column}' exists in desired state")]
    private partial void LogColumnExists(ObjectAddress owner, SqlIdentifier column);

    [LoggerMessage(EventId = 31, Level = LogLevel.Information, Message = "Column '{Owner}.{Column}' not found in desired state")]
    private partial void LogColumnNotInDesired(ObjectAddress owner, SqlIdentifier column);

    [LoggerMessage(EventId = 32, Level = LogLevel.Information, Message = "Column '{Owner}.{Column}' is new")]
    private partial void LogColumnNew(ObjectAddress owner, SqlIdentifier column);

    [LoggerMessage(EventId = 33, Level = LogLevel.Debug, Message = "Column '{Owner}.{Column}' is unchanged")]
    private partial void LogColumnUnchanged(ObjectAddress owner, SqlIdentifier column);

    [LoggerMessage(EventId = 34, Level = LogLevel.Information, Message = "Column '{Owner}.{OldName}' renamed to '{NewName}'")]
    private partial void LogColumnRenamed(ObjectAddress owner, SqlIdentifier oldName, SqlIdentifier newName);

    [LoggerMessage(EventId = 35, Level = LogLevel.Debug, Message = "Column '{Owner}.{Column}' type is unchanged ({Type})")]
    private partial void LogColumnTypeUnchanged(ObjectAddress owner, SqlIdentifier column, SqlType type);

    [LoggerMessage(EventId = 36, Level = LogLevel.Information, Message = "Column '{Owner}.{Column}' type changed: {OldType} -> {NewType}")]
    private partial void LogColumnTypeChanged(ObjectAddress owner, SqlIdentifier column, SqlType oldType, SqlType newType);

    [LoggerMessage(EventId = 37, Level = LogLevel.Debug, Message = "Column '{Owner}.{Column}' nullability is unchanged ({Nullability})")]
    private partial void LogColumnNullabilityUnchanged(ObjectAddress owner, SqlIdentifier column, string nullability);

    [LoggerMessage(EventId = 38, Level = LogLevel.Information, Message = "Column '{Owner}.{Column}' nullability changed: {OldValue} -> {NewValue}")]
    private partial void LogColumnNullabilityChanged(ObjectAddress owner, SqlIdentifier column, bool oldValue, bool newValue);

    [LoggerMessage(EventId = 39, Level = LogLevel.Debug, Message = "Column '{Owner}.{Column}' default is unchanged ({Default})")]
    private partial void LogColumnDefaultUnchanged(ObjectAddress owner, SqlIdentifier column, string @default);

    [LoggerMessage(EventId = 40, Level = LogLevel.Information, Message = "Column '{Owner}.{Column}' default changed: '{OldDefault}' -> '{NewDefault}'")]
    private partial void LogColumnDefaultChanged(ObjectAddress owner, SqlIdentifier column, SqlDefaultExpression? oldDefault, SqlDefaultExpression? newDefault);

    [LoggerMessage(EventId = 41, Level = LogLevel.Information, Message = "Column '{Owner}.{Column}' comment changed")]
    private partial void LogColumnCommentChanged(ObjectAddress owner, SqlIdentifier column);

    [LoggerMessage(EventId = 42, Level = LogLevel.Information, Message = "Column '{Owner}.{Column}' identity sequence options changed: start {OldStart} -> {NewStart}, min {OldMin} -> {NewMin}, increment {OldIncrement} -> {NewIncrement}")]
    private partial void LogColumnIdentityChanged(ObjectAddress owner, SqlIdentifier column, long? oldStart, long? newStart, long? oldMin, long? newMin, long? oldIncrement, long? newIncrement);

    [LoggerMessage(EventId = 50, Level = LogLevel.Debug, Message = "Primary key for '{Owner}' is unchanged")]
    private partial void LogPrimaryKeyUnchanged(ObjectAddress owner);

    [LoggerMessage(EventId = 51, Level = LogLevel.Information, Message = "Dropping primary key '{KeyName}' from '{Owner}'")]
    private partial void LogPrimaryKeyDropping(SqlIdentifier keyName, ObjectAddress owner);

    [LoggerMessage(EventId = 52, Level = LogLevel.Information, Message = "Adding primary key '{KeyName}' to '{Owner}'")]
    private partial void LogPrimaryKeyAdding(SqlIdentifier keyName, ObjectAddress owner);

    [LoggerMessage(EventId = 68, Level = LogLevel.Information, Message = "Primary key '{Name}' on '{Owner}' comment changed")]
    private partial void LogPrimaryKeyCommentChanged(SqlIdentifier name, ObjectAddress owner);

    // Shared by CompareTableMembers for every list-member kind (foreign keys, unique/check constraints, indexes).
    [LoggerMessage(EventId = 60, Level = LogLevel.Information, Message = "{MemberKind} '{Name}' on '{Owner}' is missing or changed")]
    private partial void LogTableMemberMissingOrChanged(string memberKind, SqlIdentifier name, ObjectAddress owner);

    [LoggerMessage(EventId = 61, Level = LogLevel.Debug, Message = "{MemberKind} '{Name}' on '{Owner}' is unchanged")]
    private partial void LogTableMemberUnchanged(string memberKind, SqlIdentifier name, ObjectAddress owner);

    [LoggerMessage(EventId = 62, Level = LogLevel.Information, Message = "{MemberKind} '{Name}' on '{Owner}' is new or changed")]
    private partial void LogTableMemberNewOrChanged(string memberKind, SqlIdentifier name, ObjectAddress owner);

    [LoggerMessage(EventId = 64, Level = LogLevel.Information, Message = "{MemberKind} '{Name}' on '{Owner}' comment changed")]
    private partial void LogTableMemberCommentChanged(string memberKind, SqlIdentifier name, ObjectAddress owner);

    [LoggerMessage(EventId = 80, Level = LogLevel.Information, Message = "Revoking USAGE on schema '{Schema}' from '{Role}'")]
    private partial void LogSchemaUsageRevoking(SqlIdentifier schema, SqlIdentifier role);

    [LoggerMessage(EventId = 81, Level = LogLevel.Information, Message = "Granting USAGE on schema '{Schema}' to '{Role}'")]
    private partial void LogSchemaUsageGranting(SqlIdentifier schema, SqlIdentifier role);

    [LoggerMessage(EventId = 82, Level = LogLevel.Information, Message = "Revoking all privileges on '{Owner}' from '{Role}'")]
    private partial void LogTablePrivilegesRevoking(ObjectAddress owner, SqlIdentifier role);

    [LoggerMessage(EventId = 83, Level = LogLevel.Information, Message = "Updating privileges on '{Owner}' for '{Role}'")]
    private partial void LogTablePrivilegesUpdating(ObjectAddress owner, SqlIdentifier role);

    [LoggerMessage(EventId = 84, Level = LogLevel.Information, Message = "Granting privileges on '{Owner}' to '{Role}'")]
    private partial void LogTablePrivilegesGranting(ObjectAddress owner, SqlIdentifier role);

    [LoggerMessage(EventId = 85, Level = LogLevel.Information, Message = "Granting privileges on new table '{Owner}' to '{Role}'")]
    private partial void LogTablePrivilegesGrantingToNewTable(ObjectAddress owner, SqlIdentifier role);
}
