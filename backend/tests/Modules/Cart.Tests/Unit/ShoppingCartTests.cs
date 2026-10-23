using AwesomeAssertions;
using FreshOrbit.Cart.Domain.Carts;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Cart.Tests.Unit;

public sealed class ShoppingCartTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 6, 0, 0, TimeSpan.Zero);
    private static readonly QuantityRules Grams = new(250, 5000, 250);
    private static readonly QuantityRules Pieces = new(1, 10, 1);
    private static readonly Guid Fish = Guid.CreateVersion7();
    private static readonly Guid Crab = Guid.CreateVersion7();
    private static readonly Guid Steaks = Guid.CreateVersion7();

    [Theory]
    [InlineData(0, "QUANTITY_BELOW_MIN")]
    [InlineData(125, "QUANTITY_BELOW_MIN")]
    [InlineData(5250, "QUANTITY_ABOVE_MAX")]
    [InlineData(600, "QUANTITY_STEP_INVALID")]
    [InlineData(250, null)]
    [InlineData(5000, null)]
    public void Quantity_rules_report_the_first_broken_rule(int quantity, string? code)
        => Grams.Check(quantity)?.Code.Should().Be(code);

    [Fact]
    public void Adding_the_same_variant_and_preparation_adds_to_the_line()
    {
        var cart = ShoppingCart.ForCustomer(Guid.NewGuid(), Now);

        var first = cart.AddItem(Fish, Steaks, 500, Grams, Now);
        var again = cart.AddItem(Fish, Steaks, 250, Grams, Now);
        cart.AddItem(Fish, null, 500, Grams, Now);

        again.Id.Should().Be(first.Id);
        cart.Items.Should().HaveCount(2);
        first.Quantity.Should().Be(750);
    }

    [Fact]
    public void Adding_beyond_max_on_an_existing_line_is_refused()
    {
        var cart = ShoppingCart.ForCustomer(Guid.NewGuid(), Now);
        cart.AddItem(Crab, null, 8, Pieces, Now);

        FluentActions.Invoking(() => cart.AddItem(Crab, null, 3, Pieces, Now))
            .Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Code == "QUANTITY_ABOVE_MAX" && e.Field == "quantity.value");
    }

    [Fact]
    public void A_cart_holds_at_most_thirty_lines()
    {
        var cart = ShoppingCart.ForCustomer(Guid.NewGuid(), Now);
        for (var i = 0; i < ShoppingCart.MaxLines; i++)
        {
            cart.AddItem(Guid.NewGuid(), null, 1, Pieces, Now);
        }

        FluentActions.Invoking(() => cart.AddItem(Guid.NewGuid(), null, 1, Pieces, Now))
            .Should().Throw<DomainException>().Which.Code.Should().Be("CART_ITEM_LIMIT");
    }

    [Fact]
    public void Changing_preparation_onto_an_existing_line_merges_capped_at_max()
    {
        var cart = ShoppingCart.ForCustomer(Guid.NewGuid(), Now);
        var whole = cart.AddItem(Fish, null, 3000, Grams, Now);
        var steaks = cart.AddItem(Fish, Steaks, 3000, Grams, Now);

        var holder = cart.ChangePreparation(whole.Id, Steaks, Grams, Now);

        holder.Id.Should().Be(steaks.Id);
        holder.Quantity.Should().Be(5000);
        cart.Items.Should().ContainSingle();
    }

    [Fact]
    public void Unknown_line_is_not_found()
    {
        var cart = ShoppingCart.ForCustomer(Guid.NewGuid(), Now);

        FluentActions.Invoking(() => cart.RemoveItem(Guid.NewGuid(), Now)).Should().Throw<NotFoundException>().Which.Code.Should().Be("CART_ITEM_NOT_FOUND");
    }

    [Fact]
    public void Guest_merge_adds_up_capped_appends_and_drops_missing_variants()
    {
        var customer = ShoppingCart.ForCustomer(Guid.NewGuid(), Now);
        customer.AddItem(Crab, null, 8, Pieces, Now);
        var guest = ShoppingCart.ForGuest("hash", Now);
        guest.AddItem(Crab, null, 5, Pieces, Now);
        guest.AddItem(Fish, Steaks, 500, Grams, Now);
        var gone = Guid.NewGuid();
        guest.AddItem(gone, null, 1, Pieces, Now);
        guest.SetLocation(Guid.NewGuid(), "600041", Now);

        var result = customer.MergeFrom(guest, v => v == gone ? null : v == Crab ? 10 : 5000, Now);

        result.Should().Be(new CartMergeResult(2, 1));
        customer.FindLine(Crab, null)!.Quantity.Should().Be(10);
        customer.FindLine(Fish, Steaks)!.Quantity.Should().Be(500);
        customer.Pincode.Should().Be("600041");
        guest.Status.Should().Be(CartStatus.Converted);
    }

    [Fact]
    public void Converted_cart_refuses_changes()
    {
        var cart = ShoppingCart.ForCustomer(Guid.NewGuid(), Now);
        cart.MarkConverted(Now);

        FluentActions.Invoking(() => cart.AddItem(Fish, null, 500, Grams, Now)).Should().Throw<NotFoundException>().Which.Code.Should().Be("CART_NOT_FOUND");
    }

    [Fact]
    public void Carts_are_abandoned_after_their_lifetime_of_inactivity()
    {
        var customer = ShoppingCart.ForCustomer(Guid.NewGuid(), Now);
        var guest = ShoppingCart.ForGuest("hash", Now);

        guest.AbandonIfDue(Now.AddDays(7).AddSeconds(-1)).Should().BeFalse();
        guest.AbandonIfDue(Now.AddDays(7)).Should().BeTrue();
        customer.AbandonIfDue(Now.AddDays(29)).Should().BeFalse();
        customer.AbandonIfDue(Now.AddDays(30)).Should().BeTrue();
        customer.Status.Should().Be(CartStatus.Abandoned);
    }

    [Fact]
    public void Coupon_is_normalized_and_cleared_with_the_cart()
    {
        var cart = ShoppingCart.ForCustomer(Guid.NewGuid(), Now);
        cart.ApplyCoupon(" fresh100 ", Now);
        cart.CouponCode.Should().Be("FRESH100");

        cart.Clear(Now);

        cart.CouponCode.Should().BeNull();
        FluentActions.Invoking(() => ShoppingCart.ForGuest("hash", Now).ApplyCoupon("FRESH100", Now)).Should().Throw<InvalidOperationException>();
    }
}
