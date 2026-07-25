using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Scripts;

namespace NSchema.Plan.Backends;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders a declared script — verbatim by default; a dialect may validate or normalize the SQL.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> ExecuteScript(ExecuteScript action) =>
        Statements(action.Statement);
}
