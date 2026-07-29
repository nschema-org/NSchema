namespace NSchema.Extensions;

internal static class CollectionExtensions
{
    extension<T>(IList<T> source)
    {
        /// <summary>
        /// Removes every item matching the predicate, back to front so removal hooks fire per item.
        /// </summary>
        public void RemoveWhere(Func<T, bool> predicate)
        {
            for (var i = source.Count - 1; i >= 0; i--)
            {
                if (predicate(source[i]))
                {
                    source.RemoveAt(i);
                }
            }
        }
    }

    extension<T>(IReadOnlyList<T> source)
    {
        /// <summary>
        /// Returns the items in dependency order (dependencies first).
        /// </summary>
        /// <typeparam name="TKey">The identity type; equality semantics live on the key (e.g. identifier tuples).</typeparam>
        /// <param name="key">Projects an item's identity.</param>
        /// <param name="dependencies">Projects the identities an item depends on.</param>
        /// <param name="describe">Renders an item's identity for the cycle error message.</param>
        /// <param name="allowCycles">Whether a cycle is legal, and so is broken at the edge that closes it rather than reported.</param>
        /// <exception cref="InvalidOperationException">A dependency cycle exists among the items.</exception>
        /// <remarks>
        /// Only dependencies that resolve to another item in the same set produce an edge; a dependency on
        /// something outside the set is ignored. The sort is stable: independent items keep their original
        /// relative order.
        /// </remarks>
        public IReadOnlyList<T> OrderedByDependencies<TKey>(
            Func<T, TKey> key,
            Func<T, IEnumerable<TKey>> dependencies,
            Func<T, string> describe,
            bool allowCycles = false
        ) where TKey : notnull
        {
            if (source.Count <= 1)
            {
                return source;
            }

            var byKey = new Dictionary<TKey, T>();
            foreach (var item in source)
            {
                // A duplicate key would make ordering ambiguous; the first declaration wins (callers dedupe upstream).
                byKey.TryAdd(key(item), item);
            }

            var ordered = new List<T>(source.Count);
            var state = new Dictionary<TKey, SortMark>();

            // Depth-first post-order over the original sequence preserves the input order for independent items.
            foreach (var item in source)
            {
                Visit(item);
            }

            return ordered;

            void Visit(T item)
            {
                var k = key(item);
                if (state.TryGetValue(k, out var mark))
                {
                    if (mark == SortMark.InProgress && !allowCycles)
                    {
                        throw new InvalidOperationException(
                            $"Dependency cycle detected involving {describe(item)}. Cyclic definitions cannot be ordered.");
                    }
                    return; // already emitted
                }

                state[k] = SortMark.InProgress;
                foreach (var dependencyKey in dependencies(item))
                {
                    if (byKey.TryGetValue(dependencyKey, out var dependency))
                    {
                        Visit(dependency);
                    }
                }
                state[k] = SortMark.Done;
                ordered.Add(item);
            }
        }
    }

    private enum SortMark
    {
        InProgress,
        Done,
    }
}
