using FreshOrbit.SharedKernel.Tests;

// One database for the assembly. The outbox tests dispatch every pending row, so test classes run one at a time
// (parallelizeTestCollections = false in xunit.runner.json).
[assembly: AssemblyFixture(typeof(SharedKernelDatabase))]
