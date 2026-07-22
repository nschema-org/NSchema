// Configuration resolution reads and mutates process-global state — environment variables and, via --directory, the current
// working directory — so tests must not run in parallel. Disable it assembly-wide rather than per-collection.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
