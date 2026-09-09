using Xunit.Sdk;
using Xunit.v3;

// The logging seam is a process-wide static: a test installs a factory, asserts against what it
// recorded, and clears it again. Parallel test classes race on it, and a class that clears the
// factory mid-assert empties another's recorder, so run the suite serially.
[assembly: Parallelization(Mode = ParallelMode.None)]
