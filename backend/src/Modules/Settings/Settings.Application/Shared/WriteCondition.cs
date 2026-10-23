using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Settings.Application.Shared;

/// <summary>
/// The HTTP precondition of a create-or-update: <c>If-Match: "v"</c> (update that version) or
/// <c>If-None-Match: *</c> (create; refused when the row exists).
/// </summary>
public sealed record WriteCondition(uint? ExpectedVersion, bool CreateOnly)
{
    public const string VersionRequiredCode = "VERSION_REQUIRED";

    /// <summary>Applies the condition to the loaded row (or null) and says whether a new row must be created.</summary>
    public bool ShouldCreate(IAggregateRoot? existing, IUnitOfWork unitOfWork, string what)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        if (existing is null)
        {
            if (CreateOnly)
            {
                return true;
            }

            throw ExpectedVersion is null
                ? new DomainException(VersionRequiredCode, "Send If-None-Match: * to create the record.", "If-None-Match")
                : new NotFoundException($"No {what} exists yet; create it with If-None-Match: *.");
        }

        if (CreateOnly)
        {
            throw new ConflictException("DUPLICATE", $"The {what} already exists; update it with If-Match.");
        }

        if (ExpectedVersion is not { } version)
        {
            throw new DomainException(VersionRequiredCode, "Send the current version of the record in the If-Match header.", "If-Match");
        }

        unitOfWork.SetExpectedVersion(existing, version);
        return false;
    }
}
