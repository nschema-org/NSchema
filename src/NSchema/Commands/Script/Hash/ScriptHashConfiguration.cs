using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration;

namespace NSchema.Commands.Script.Hash;

/// <summary>
/// Configuration for <c>script hash</c> — the command reads only the project's DDL, so there is nothing to bind yet.
/// </summary>
internal sealed class ScriptHashConfiguration : IBindable
{
    public void Bind(ProjectConfig project, ParseResult cli)
    {
    }
}
