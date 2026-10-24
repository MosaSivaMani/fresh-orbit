using AwesomeAssertions;
using FreshOrbit.Reporting.Infrastructure.Reports;

namespace FreshOrbit.Reporting.Tests;

/// <summary>G4 sales tile rule: average order value over orders that were not cancelled.</summary>
public sealed class DashboardSalesTests
{
    [Fact]
    public void Average_order_value_excludes_cancelled_orders_and_truncates()
        => DashboardSales.Create(ordersPlaced: 5, ordersCancelled: 2, orderValuePaise: 405_305, ordersDelivered: 3, deliveredRevenuePaise: 405_305)
            .AverageOrderValuePaise.Should().Be(135_101);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 2)]
    public void Average_order_value_is_null_without_kept_orders(long placed, long cancelled)
        => DashboardSales.Create(placed, cancelled, 0, 0, 0).AverageOrderValuePaise.Should().BeNull();
}
