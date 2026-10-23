using FreshOrbit.Testing;

// One database for the assembly; test classes run one at a time (xunit.runner.json).
[assembly: AssemblyFixture(typeof(PostgresFixture))]
