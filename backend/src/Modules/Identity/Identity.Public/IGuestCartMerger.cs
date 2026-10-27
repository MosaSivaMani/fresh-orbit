namespace FreshOrbit.Identity.Public;

/// <summary>
/// Implemented by the Cart module (which uses Identity): merges a guest cart into the customer's cart right after an
/// OTP login has committed, so <c>POST /auth/otp/verify</c> can return the merged cart. It runs in its own
/// transaction after the login's; the <c>CustomerSignedIn</c> handler later finds the guest cart already merged and
/// does nothing. Not registered → the login answers <c>cart: null</c> and the handler merges.
/// </summary>
public interface IGuestCartMerger
{
    /// <summary>The customer's cart after the merge; null when the token names no ACTIVE guest cart.</summary>
    Task<GuestCartMerge?> MergeAsync(string guestCartToken, Guid customerId, CancellationToken cancellationToken = default);
}

/// <param name="CartId">The customer's ACTIVE cart that received the lines.</param>
/// <param name="MergedLineCount">Guest lines added to (or summed into) the customer's cart.</param>
/// <param name="DroppedLineCount">Guest lines that could not be kept (variant gone, or the cart line limit reached).</param>
public sealed record GuestCartMerge(Guid CartId, int MergedLineCount, int DroppedLineCount);
