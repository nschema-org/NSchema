namespace NSchema.Extensions;

/// <summary>
/// One ordering constraint between two items of a list, by position: the dependent may not precede its
/// dependency. <paramref name="Strength"/> ranks how much the edge is trusted — a cycle is broken at its
/// weakest edge first.
/// </summary>
internal readonly record struct DependencyEdge(int Dependent, int Dependency, int Strength);

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
        /// Returns the items in dependency order, with tie-breakers chosen by <paramref name="priority"/>, then input position.
        /// </summary>
        /// <param name="priority">Ranks an item; lower runs earlier among the unblocked.</param>
        /// <param name="edges">The constraints, by list position; an out-of-range or self edge is ignored.</param>
        /// <remarks>
        /// Cycles are broken rather than reported: when every remaining item is blocked, the item whose
        /// unsatisfied edges are weakest (then best priority, then first declared) is released and those
        /// edges discarded. It's deterministic, and it never fails a plan on an inferred guess.
        /// </remarks>
        public IReadOnlyList<T> OrderedByDependencies(Func<T, long> priority, IReadOnlyList<DependencyEdge> edges)
        {
            if (source.Count <= 1)
            {
                return source;
            }

            var dependentsOf = new List<int>?[source.Count];
            var blockedBy = new List<DependencyEdge>?[source.Count];
            foreach (var edge in edges)
            {
                if (edge.Dependent == edge.Dependency
                    || edge.Dependent < 0 || edge.Dependent >= source.Count
                    || edge.Dependency < 0 || edge.Dependency >= source.Count)
                {
                    continue;
                }

                (dependentsOf[edge.Dependency] ??= []).Add(edge.Dependent);
                (blockedBy[edge.Dependent] ??= []).Add(edge);
            }

            var remaining = new int[source.Count];
            var ready = new PriorityQueue<int, (long Priority, int Position)>();
            for (var i = 0; i < source.Count; i++)
            {
                remaining[i] = blockedBy[i]?.Count ?? 0;
                if (remaining[i] == 0)
                {
                    ready.Enqueue(i, (priority(source[i]), i));
                }
            }

            var ordered = new List<T>(source.Count);
            var emitted = new bool[source.Count];
            while (ordered.Count < source.Count)
            {
                if (ready.Count == 0)
                {
                    Release(BestBlocked());
                }

                var index = ready.Dequeue();
                if (emitted[index])
                {
                    continue;
                }

                emitted[index] = true;
                ordered.Add(source[index]);
                foreach (var dependent in dependentsOf[index] ?? (IEnumerable<int>)[])
                {
                    if (--remaining[dependent] == 0 && !emitted[dependent])
                    {
                        ready.Enqueue(dependent, (priority(source[dependent]), dependent));
                    }
                }
            }

            return ordered;

            // The blocked item held by the weakest edges: cutting there costs the least confidence.
            int BestBlocked()
            {
                var best = -1;
                var bestKey = (Strength: int.MaxValue, Priority: long.MaxValue, Position: int.MaxValue);
                for (var i = 0; i < source.Count; i++)
                {
                    if (emitted[i] || remaining[i] == 0)
                    {
                        continue;
                    }

                    var strength = blockedBy[i]!.Where(e => !emitted[e.Dependency]).Max(e => e.Strength);
                    var key = (strength, priority(source[i]), i);
                    if (key.CompareTo(bestKey) < 0)
                    {
                        best = i;
                        bestKey = key;
                    }
                }

                return best;
            }

            void Release(int index)
            {
                remaining[index] = 0;
                ready.Enqueue(index, (priority(source[index]), index));
            }
        }
    }
}
