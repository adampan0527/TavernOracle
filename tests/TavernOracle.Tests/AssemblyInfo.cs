using Xunit;

// Disable parallel test execution within the test assembly to ensure deterministic,
// zero-interference GC and zero-allocation performance benchmarks.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
