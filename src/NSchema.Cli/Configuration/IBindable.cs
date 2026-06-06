using System.CommandLine;

namespace NSchema.Cli.Configuration;

internal interface IBindable
{
    void Bind(ParseResult result);
}
