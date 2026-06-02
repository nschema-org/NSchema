using NSchema.Cli.Commands;

var root = RootCommand.Create();
var result = root.Parse(args);
return await result.InvokeAsync();
