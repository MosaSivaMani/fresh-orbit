using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace FreshOrbit.SharedKernel.EntityFramework;

/// <summary>Caches one EF model per distinct set of module models (tests may add test-only models).</summary>
internal sealed class FreshOrbitModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => context is FreshOrbitDbContext freshOrbit
            ? (context.GetType(), freshOrbit.ModelKey, designTime)
            : (context.GetType(), designTime);
}
