using FreshOrbit.Ordering.Tests.Integration;

// One database for the assembly; test classes run one at a time (xunit.runner.json).
[assembly: AssemblyFixture(typeof(OrderingDatabase))]
