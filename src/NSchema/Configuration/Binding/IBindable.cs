using System.CommandLine;

namespace NSchema.Cli.Configuration.Binding;

internal interface IBindable
{
    void Bind(ParseResult result);
}
