using AwesomeAssertions;
using FreshOrbit.Ordering.Domain.Complaints;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Storage;

namespace FreshOrbit.Ordering.Tests.Unit;

/// <summary>W12 photo rules and the G15 refund bound.</summary>
public sealed class ComplaintPhotoTests
{
    private static readonly DateTimeOffset DeliveredAt = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid CustomerId = Guid.NewGuid();

    [Fact]
    public void Open_complaint_takes_up_to_four_photos_then_refuses()
    {
        var complaint = Raise();
        for (var i = 0; i < Complaint.MaxPhotos; i++)
        {
            var id = Guid.NewGuid();
            complaint.AddPhoto(id, CustomerId, Complaint.PhotoKey(complaint.Id, id), 800, 600, 1234, DeliveredAt.AddHours(2));
        }

        var fifth = () => complaint.AddPhoto(Guid.NewGuid(), CustomerId, "complaints/x/y", 800, 600, 1234, DeliveredAt.AddHours(2));

        fifth.Should().Throw<DomainException>().Which.Code.Should().Be("COMPLAINT_PHOTO_LIMIT_REACHED");
        complaint.Photos.Should().HaveCount(Complaint.MaxPhotos);
    }

    [Fact]
    public void Photos_are_refused_after_the_decision_and_for_another_customer()
    {
        var complaint = Raise();
        var stranger = () => complaint.EnsureCanAddPhoto(Guid.NewGuid());
        stranger.Should().Throw<NotFoundException>();

        complaint.Decide(ComplaintStatus.Rejected, ComplaintResolution.None, null, 0, null, Guid.NewGuid(), DeliveredAt.AddHours(3));
        var late = () => complaint.EnsureCanAddPhoto(CustomerId);

        late.Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_STATUS_TRANSITION");
    }

    [Fact]
    public void Photo_keys_are_valid_storage_keys_without_an_extension()
    {
        var key = Complaint.PhotoKey(Guid.NewGuid(), Guid.NewGuid());

        StorageKeys.IsValid(key).Should().BeTrue();
        Path.GetExtension(key).Should().BeEmpty();
    }

    [Theory]
    [InlineData(72306L, 1000L, 71306L)]
    [InlineData(null, null, 0L)]
    [InlineData(500L, 900L, 0L)]
    public void Line_refund_bound_is_final_total_less_final_discount_never_negative(long? total, long? discount, long expected)
        => ComplaintRefundLimit.ForLine(total, discount).Should().Be(expected);

    [Fact]
    public void Order_refund_bound_uses_the_final_total_once_packed()
    {
        ComplaintRefundLimit.ForOrder(245051, 234900).Should().Be(245051);
        ComplaintRefundLimit.ForOrder(null, 234900).Should().Be(234900);
    }

    private static Complaint Raise()
        => Complaint.Raise(new ComplaintOrderFacts(Guid.NewGuid(), "FO-260001", CustomerId, Guid.NewGuid(), DeliveredAt), Guid.NewGuid(), ComplaintKind.Damaged, "The crab arrived crushed.", 24, false, DeliveredAt.AddHours(1));
}
