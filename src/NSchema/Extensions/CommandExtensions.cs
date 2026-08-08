using System.CommandLine;
using System.CommandLine.Parsing;

namespace NSchema.Extensions;

internal static class CommandExtensions
{
    extension<T>(IList<T> list)
    {
        /// <summary>
        /// Adds a range of items to the list.
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                list.Add(item);
            }
        }
    }

    extension(CommandResult result)
    {
        /// <summary>
        /// Whether the user actually wrote <paramref name="option"/>, as opposed to it being filled in from its
        /// default. What a usage-error validator must ask: a defaulted flag was never asked for.
        /// </summary>
        public bool Specified(Option option) => result.GetResult(option) is { Implicit: false };
    }
}
