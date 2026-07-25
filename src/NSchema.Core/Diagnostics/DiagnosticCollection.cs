using System.Collections;

namespace NSchema.Diagnostics;

/// <summary>
/// An ordered set of diagnostics.
/// </summary>
/// <typeparam name="TDiagnostic">The diagnostic type the producer mints.</typeparam>
public class DiagnosticCollection<TDiagnostic> : IDiagnosticCollection<TDiagnostic> where TDiagnostic : Diagnostic
{
    private readonly List<TDiagnostic> _items;

    /// <summary>
    /// An empty collection.
    /// </summary>
    public DiagnosticCollection() => _items = [];

    /// <summary>
    /// A collection seeded with a copy of <paramref name="diagnostics"/>.
    /// </summary>
    public DiagnosticCollection(IEnumerable<TDiagnostic> diagnostics) => _items = [.. diagnostics];

    /// <inheritdoc />
    public int Count => _items.Count;

    /// <inheritdoc />
    public TDiagnostic this[int index] => _items[index];

    /// <inheritdoc />
    public bool HasErrors => _items.Any(d => d.Severity == DiagnosticSeverity.Error);

    /// <inheritdoc />
    public IEnumerable<TDiagnostic> Errors => _items.Where(d => d.Severity == DiagnosticSeverity.Error);

    /// <inheritdoc />
    public IEnumerable<TDiagnostic> Warnings => _items.Where(d => d.Severity == DiagnosticSeverity.Warning);

    /// <summary>
    /// Adds a single finding.
    /// </summary>
    public void Add(TDiagnostic diagnostic) => _items.Add(diagnostic);

    /// <summary>
    /// Adds a set of findings.
    /// </summary>
    public void Add(IEnumerable<TDiagnostic> diagnostics) => _items.AddRange(diagnostics);

    /// <summary>
    /// Downgrades every finding above <paramref name="severity"/> to it, in place.
    /// </summary>
    public void Demote(DiagnosticSeverity severity)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            _items[i] = (TDiagnostic)_items[i].Demote(severity);
        }
    }

    /// <inheritdoc />
    public IEnumerator<TDiagnostic> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// A <see cref="DiagnosticCollection{TDiagnostic}"/> of the base <see cref="Diagnostic"/> — the common case.
/// </summary>
public class DiagnosticCollection : DiagnosticCollection<Diagnostic>
{
    /// <summary>
    /// An empty collection.
    /// </summary>
    public DiagnosticCollection()
    {
    }

    /// <summary>
    /// A collection seeded with a copy of <paramref name="diagnostics"/>.
    /// </summary>
    public DiagnosticCollection(IEnumerable<Diagnostic> diagnostics) : base(diagnostics)
    {
    }
}
