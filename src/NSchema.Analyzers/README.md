# NSchema.Analyzers

The engine's architecture rules, enforced by the compiler instead of by a test run.

Violations show up as build errors and as squiggles in the IDE, on the line that causes them.

## How it is wired

- `NSchema.Core.csproj` references this project with `OutputItemType="Analyzer"`.
  That hands the built DLL to the compiler rather than linking against it.
- `ReferenceOutputAssembly="false"` means Core cannot call into it.
- `PrivateAssets="all"` keeps it out of the published `NSchema.Core` package.
- Nothing else is needed — no NuGet package, no MSBuild target.

Rider and Visual Studio load the analyzer from the last build. After changing a rule, rebuild;
if the IDE still shows stale results, restart it (it caches analyzer assemblies).

## The pieces

| File                                | What it is                                                                  |
|-------------------------------------|-----------------------------------------------------------------------------|
| `Architecture.cs`                   | The layering table. **Edit this to add an edge.**                           |
| `Namespaces.cs`                     | Reads a namespace and says which slice and what shape of thing it is.       |
| `Rules.cs`                          | The four `DiagnosticDescriptor`s — id, severity, and the sentence template. |
| `Reference.cs`                      | Resolves a name in the source to "this type mentions that type".            |
| `ArchitectureDependencyAnalyzer.cs` | Reports NS0001–NS0003.                                                      |
| `UndeclaredSliceAnalyzer.cs`        | Reports NS0004.                                                             |

## The rules

| Id     | Says                                                                       |
|--------|----------------------------------------------------------------------------|
| NS0001 | A slice referenced another slice its row in the table does not list.       |
| NS0002 | A slice's domain reached for the application services that orchestrate it. |
| NS0003 | A provider seam reached for `Operations`.                                  |
| NS0004 | A namespace introduced a top-level slice the table has never heard of.     |

They are `Warning`, not `Error`, so one can be dialled down from `.editorconfig` while a refactor is in
flight. Core builds with `TreatWarningsAsErrors`, so in practice a violation still fails the build.

```ini
# .editorconfig — quieten one rule temporarily
dotnet_diagnostic.NS0001.severity = suggestion
```

## Writing an analyzer, if you have not before

An analyzer is a class that subscribes to things the compiler sees and reports diagnostics about them.

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]                       // 1. found by the compiler
public sealed class MyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rules.Something);                // 2. what it may report

    public override void Initialize(AnalysisContext context)     // 3. runs once per compilation
    {
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IdentifierName);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        // 4. runs per node, in parallel, and must not hold state between calls
        context.ReportDiagnostic(Diagnostic.Create(Rules.Something, context.Node.GetLocation()));
    }
}
```

Four things worth knowing:

- **Syntax is what was written; symbols are what it means.** `context.Node` is syntax.
  `context.SemanticModel.GetSymbolInfo(node).Symbol` turns a name into the thing it refers to.
- **Analyzers must be stateless.** They run concurrently across files and are reused between
  compilations. Anything remembered between callbacks is a bug.
- **Every reference to a type is a name in the source**, so subscribing to `IdentifierName` and
  `GenericName` is enough to see base types, parameters, generic arguments, attributes and calls alike.
- **`SupportedDiagnostics` must list everything you report**, or the report is dropped.

The project targets `netstandard2.0` because analyzers load into the compiler, which may be running
on .NET Framework. `EnforceExtendedAnalyzerRules` turns on the checks for that — no `System.IO`, no
`Environment`, nothing that assumes a process of your own.

## Adding a rule

1. Add a `DiagnosticDescriptor` to `Rules.cs` with the next free id.
2. Report it from an existing analyzer, or add a new class if it subscribes to different syntax.
3. Add it to that analyzer's `SupportedDiagnostics`.
4. Cover it in `tests/NSchema.Analyzers.Tests` — one test showing it fire, one showing it stay quiet.

Ids NS0001–NS0099 are the architecture rules. Later families start at NS0100.

## Testing

`tests/NSchema.Analyzers.Tests` compiles snippets in memory and runs an analyzer over them. There is no
analyzer testing package — `CSharpCompilation.Create(...).WithAnalyzers(...)` is the whole mechanism, and
`Analysis.Run` is the thirty lines that wrap it.

The harness fails a test whose snippet does not compile. A snippet with an error resolves no symbols, so
every rule would pass by saying nothing at all.

`ArchitectureTests` covers what the analyzers cannot see — the table's own integrity. It is acyclic, it
names only slices that exist, and every row still has code behind it. That last one matters: a row left
behind after its folder was renamed away governs nothing while looking as though it governs a slice.
