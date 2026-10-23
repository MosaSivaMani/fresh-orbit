using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.SharedKernel.Web.Errors;

/// <summary>An update was sent without the If-Match version (HTTP 428 VERSION_REQUIRED).</summary>
public sealed class VersionRequiredException : DomainException
{
    public const string DefaultCode = "VERSION_REQUIRED";

    public VersionRequiredException()
        : base(DefaultCode, "Send the current version of the record in the If-Match header.", "If-Match")
    {
    }
}
