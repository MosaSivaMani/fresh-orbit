using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Tests.TestModule;
using FreshOrbit.Testing;

namespace FreshOrbit.SharedKernel.Tests;

/// <summary>The test database with every module plus the test-only <c>test</c> schema.</summary>
public sealed class SharedKernelDatabase : PostgresFixture
{
    protected override IEnumerable<IModuleModel> AdditionalModuleModels => [new WidgetModuleModel()];
}
