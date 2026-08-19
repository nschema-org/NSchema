using NSchema.Model;

namespace NSchema.Diff.Domain.Services;

/// <summary>
/// Matches named elements while preserving both collections' order.
/// </summary>
internal static class NamedEntityMatcher
{
    public static IReadOnlyDictionary<SqlIdentifier, T> FirstByName<T>(IReadOnlyList<T> entities)
        where T : DatabaseElement
    {
        var byName = new Dictionary<SqlIdentifier, T>();
        foreach (var entity in entities)
        {
            byName.TryAdd(entity.Name, entity);
        }

        return byName;
    }

    public static (T?[] ForDesired, T?[] ForCurrent) Match<T>(
        IReadOnlyList<T> current,
        IReadOnlyList<T> desired
    ) where T : DatabaseElement
    {
        var unmatchedCurrent = new Dictionary<SqlIdentifier, Queue<int>>();
        for (var index = 0; index < current.Count; index++)
        {
            if (!unmatchedCurrent.TryGetValue(current[index].Name, out var indexes))
            {
                unmatchedCurrent[current[index].Name] = indexes = [];
            }

            indexes.Enqueue(index);
        }

        var forDesired = new T?[desired.Count];
        var forCurrent = new T?[current.Count];
        for (var index = 0; index < desired.Count; index++)
        {
            if (unmatchedCurrent.TryGetValue(desired[index].Name, out var indexes)
                && indexes.TryDequeue(out var currentIndex))
            {
                forDesired[index] = current[currentIndex];
                forCurrent[currentIndex] = desired[index];
            }
        }

        return (forDesired, forCurrent);
    }
}
