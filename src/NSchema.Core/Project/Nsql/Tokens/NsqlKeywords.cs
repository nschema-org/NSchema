using System.Reflection;

namespace NSchema.Project.Nsql.Tokens;

/// <summary>
/// The vocabulary of the NSchema language.
/// </summary>
internal static class NsqlKeywords
{
    public const string Action = "ACTION";
    public const string Add = "ADD";
    public const string After = "AFTER";
    public const string Alter = "ALTER";
    public const string Always = "ALWAYS";
    public const string Apply = "APPLY";
    public const string As = "AS";
    public const string Asc = "ASC";
    public const string Before = "BEFORE";
    public const string Begin = "BEGIN";
    public const string Cache = "CACHE";
    public const string Cascade = "CASCADE";
    public const string Check = "CHECK";
    public const string Column = "COLUMN";
    public const string Constraint = "CONSTRAINT";
    public const string Create = "CREATE";
    public const string Cycle = "CYCLE";
    public const string Database = "DATABASE";
    public const string Default = "DEFAULT";
    public const string Delete = "DELETE";
    public const string Deployment = "DEPLOYMENT";
    public const string Desc = "DESC";
    public const string Domain = "DOMAIN";
    public const string Each = "EACH";
    public const string End = "END";
    public const string Engine = "ENGINE";
    public const string Enum = "ENUM";
    public const string Exclude = "EXCLUDE";
    public const string Execute = "EXECUTE";
    public const string Extension = "EXTENSION";
    public const string First = "FIRST";
    public const string For = "FOR";
    public const string Foreign = "FOREIGN";
    public const string Aggregate = "AGGREGATE";
    public const string Function = "FUNCTION";
    public const string Generated = "GENERATED";
    public const string Grant = "GRANT";
    public const string Identity = "IDENTITY";
    public const string In = "IN";
    public const string Include = "INCLUDE";
    public const string Increment = "INCREMENT";
    public const string Index = "INDEX";
    public const string Insert = "INSERT";
    public const string Instead = "INSTEAD";
    public const string Key = "KEY";
    public const string Last = "LAST";
    public const string Lock = "LOCK";
    public const string Materialized = "MATERIALIZED";
    public const string MaxValue = "MAXVALUE";
    public const string MinValue = "MINVALUE";
    public const string No = "NO";
    public const string Not = "NOT";
    public const string Null = "NULL";
    public const string Nulls = "NULLS";
    public const string Of = "OF";
    public const string On = "ON";
    public const string Once = "ONCE";
    public const string Or = "OR";
    public const string Plugin = "PLUGIN";
    public const string Post = "POST";
    public const string Pre = "PRE";
    public const string Primary = "PRIMARY";
    public const string Procedure = "PROCEDURE";
    public const string References = "REFERENCES";
    public const string Rename = "RENAME";
    public const string Routine = "ROUTINE";
    public const string Row = "ROW";
    public const string Run = "RUN";
    public const string Schema = "SCHEMA";
    public const string Script = "SCRIPT";
    public const string Select = "SELECT";
    public const string Sequence = "SEQUENCE";
    public const string Set = "SET";
    public const string State = "STATE";
    public const string Start = "START";
    public const string Statement = "STATEMENT";
    public const string Stored = "STORED";
    public const string Table = "TABLE";
    public const string Template = "TEMPLATE";
    public const string To = "TO";
    public const string Trigger = "TRIGGER";
    public const string Truncate = "TRUNCATE";
    public const string Type = "TYPE";
    public const string Unique = "UNIQUE";
    public const string Unless = "UNLESS";
    public const string Update = "UPDATE";
    public const string Usage = "USAGE";
    public const string Using = "USING";
    public const string Version = "VERSION";
    public const string View = "VIEW";
    public const string When = "WHEN";
    public const string Where = "WHERE";
    public const string With = "WITH";

    /// <summary>
    /// How keywords compare: bare words are case-insensitive.
    /// </summary>
    public static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// The keywords that open a project-grammar statement.
    /// </summary>
    public static readonly IReadOnlySet<string> StatementOpeners = Group(Create, Grant, Template, Apply, Script, Rename);

    /// <summary>
    /// The keywords that open a settings statement.
    /// </summary>
    public static readonly IReadOnlySet<string> SettingsStatementOpeners = Group(Plugin, Engine, Database, State);

    /// <summary>
    /// The keywords that open a table member.
    /// </summary>
    public static readonly IReadOnlySet<string> MemberOpeners = Group(Constraint, Unique, Index, Include);

    /// <summary>
    /// Every keyword in the vocabulary — the string constants of this class.
    /// </summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        typeof(NsqlKeywords).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!),
        Comparer);

    private static HashSet<string> Group(params string[] keywords) => new(keywords, Comparer);
}
