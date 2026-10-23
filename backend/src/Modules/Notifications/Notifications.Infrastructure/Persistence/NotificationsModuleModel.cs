using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Notifications.Infrastructure.Persistence;

/// <summary>The Notifications module's tables, all in the <c>notifications</c> schema.</summary>
public sealed class NotificationsModuleModel : IModuleModel
{
    public const string SchemaName = "notifications";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsModuleModel).Assembly);
    }
}
