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
}
