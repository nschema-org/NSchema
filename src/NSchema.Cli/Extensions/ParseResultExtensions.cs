// using System.CommandLine;
// using System.Diagnostics.CodeAnalysis;
//
// namespace NSchema.Cli.Configuration;
//
// internal static class ParseResultExtensions
// {
//     extension(ParseResult result)
//     {
//         internal bool TryGetOverride<T>(Option<T> option, [NotNullWhen(true)] out T? value)
//             => result.TryGetOverride(option, null, null, out value);
//
//         internal bool TryGetOverride(Option<string> option, string? envVar, [NotNullWhen(true)] out string? value)
//             => result.TryGetOverride<string>(option, envVar, s => s, out value);
//
//         internal bool TryGetOverride<T>(Option<T> option, string? envVar, Func<string, T>? envParser, out T? value)
//         {
//             if (result.GetResult(option) is { Implicit: false } argument)
//             {
//                 value = argument.GetRequiredValue(option);
//                 return true;
//             }
//
//             if (envVar != null && Environment.GetEnvironmentVariable(envVar) is { } envValue)
//             {
//                 if (envParser == null)
//                 {
//                     throw new InvalidOperationException("Environment variable override specified without value parser.");
//                 }
//
//                 value = envParser(envValue);
//                 return true;
//             }
//
//             value = default;
//             return false;
//         }
//     }
// }
