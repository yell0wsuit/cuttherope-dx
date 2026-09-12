using Xunit.Sdk;
using Xunit.v3;

// The parity tests swap PlatformServices.Render and draw into shared Skia surfaces, so parallel
// test classes would race on that global. Run the suite serially.
[assembly: Parallelization(Mode = ParallelMode.None)]
