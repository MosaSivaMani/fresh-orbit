namespace FreshOrbit.Identity.Public;

/// <summary>
/// Implemented by the Notifications module (which uses Identity): sends a one-time password by SMS now.
/// The code must not be stored or logged outside the development SMS sender.
/// </summary>
public interface IOtpSender
{
    Task SendAsync(OtpMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Stored as LOGIN / VERIFY_PHONE.</summary>
public enum OtpPurpose
{
    Login,
    VerifyPhone,
}

/// <param name="Phone">E.164 phone number.</param>
public sealed record OtpMessage(string Phone, OtpPurpose Purpose, string Code, int ExpiresInMinutes);
