using NSchema.Tests.Fixtures;

// Config resolution reads and mutates process-global state — environment variables and, via --directory, the current
// working directory — so tests must not run in parallel. Disable it assembly-wide rather than per-collection.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// One MinIO container shared across every test class (injectable alongside each provider's own collection fixture),
// backing the s3 state-store tests in the round-trip matrix.
[assembly: AssemblyFixture(typeof(MinioFixture))]
