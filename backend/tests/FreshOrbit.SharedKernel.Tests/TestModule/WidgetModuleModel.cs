using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.SharedKernel.Tests.TestModule;

public sealed class WidgetModuleModel : IModuleModel
{
    public string Schema => "test";

    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Widget>(b =>
        {
            b.ToTable("widgets");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(100);
        });
        modelBuilder.Entity<WidgetLog>(b =>
        {
            b.ToTable("widget_logs");
            b.HasKey(x => x.Id);
        });
    }
}
