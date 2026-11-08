namespace FreshOrbit.Notifications.Application.Shared;

/// <summary>E-mail addresses in logs show only the first character of the local part and the domain (D322, DPDP).</summary>
public static class EmailMasking
{
    /// <summary>
    /// <c>ravi.kumar@example.com</c> → <c>r***@example.com</c>; a value without a local part and a domain is fully
    /// masked (<c>***</c>).
    /// </summary>
    public static string Mask(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return string.Empty;
        }

        var at = email.LastIndexOf('@');
        return at <= 0 || at == email.Length - 1 ? "***" : email[0] + "***" + email[at..];
    }
}
