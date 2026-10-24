namespace FreshOrbit.Notifications.Application.Shared;

/// <summary>Phone numbers in logs show only their last 4 digits (SEC-1, DPDP).</summary>
public static class PhoneMasking
{
    /// <summary><c>+919876543210</c> → <c>***3210</c>; 4 characters or fewer are fully masked.</summary>
    public static string Mask(string? phone)
    {
        if (string.IsNullOrEmpty(phone))
        {
            return string.Empty;
        }

        return phone.Length <= 4 ? new string('*', phone.Length) : "***" + phone[^4..];
    }
}
