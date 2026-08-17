// Configuration resolution reads and mutates process-global state — environment variables and, via --directory, the current
// working directory — so tests must not run in parallel. Disable it assembly-wide rather than per-collection.

using Xunit.Sdk;
using Xunit.v3;

[assembly: Parallelization(Mode = ParallelMode.None)]
