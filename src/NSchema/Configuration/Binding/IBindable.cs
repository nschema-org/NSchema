using System.CommandLine;

namespace NSchema.Configuration.Binding;

internal interface IBindable
{
    void Bind(ParseResult result);
}
