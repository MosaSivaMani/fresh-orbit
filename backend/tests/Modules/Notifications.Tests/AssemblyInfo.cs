using FreshOrbit.E1Testing;

// One database (with reference data) for the assembly; test classes run one at a time (xunit.runner.json).
[assembly: AssemblyFixture(typeof(E1Database))]
