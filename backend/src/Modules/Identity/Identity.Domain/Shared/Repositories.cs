using FreshOrbit.Identity.Domain.Otp;
using FreshOrbit.Identity.Domain.Roles;
using FreshOrbit.Identity.Domain.Staff;
using FreshOrbit.Identity.Domain.Tokens;
using FreshOrbit.Identity.Domain.Users;

namespace FreshOrbit.Identity.Domain.Shared;

public interface IUserRepository
{
    Task<User?> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<User?> FindByPhoneAsync(string phone, CancellationToken cancellationToken);

    Task<User?> FindStaffByEmailAsync(string email, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    void Add(User user);
}

public interface IStaffLoginFailureRepository
{
    /// <summary>Times of the failures for this e-mail hash and client IP after <paramref name="since"/>, oldest first.</summary>
    Task<IReadOnlyList<DateTimeOffset>> GetFailureTimesAsync(string emailHash, string clientIp, DateTimeOffset since, CancellationToken cancellationToken);

    void Add(StaffLoginFailure failure);
}

public interface IOtpChallengeRepository
{
    Task<OtpChallenge?> GetAsync(Guid challengeId, CancellationToken cancellationToken);

    /// <summary>Request times of the phone's challenges since <paramref name="since"/>, newest first.</summary>
    Task<IReadOnlyList<DateTimeOffset>> GetRequestTimesForPhoneAsync(string phone, DateTimeOffset since, CancellationToken cancellationToken);

    Task<IReadOnlyList<DateTimeOffset>> GetRequestTimesForIpAsync(string ip, DateTimeOffset since, CancellationToken cancellationToken);

    void Add(OtpChallenge challenge);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> GetActiveFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(RefreshToken token);
}

public interface IStaffRepository
{
    Task<StaffMember?> GetAsync(Guid staffMemberId, CancellationToken cancellationToken);

    Task<StaffMember?> FindByUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Number of active staff members, other than <paramref name="exceptStaffMemberId"/>, holding <paramref name="roleId"/>.</summary>
    Task<int> CountActiveWithRoleAsync(Guid roleId, Guid? exceptStaffMemberId, CancellationToken cancellationToken);

    Task<bool> AnyWithRoleAsync(Guid roleId, CancellationToken cancellationToken);

    void Add(StaffMember staff);
}

public interface IRoleRepository
{
    Task<Role?> GetAsync(Guid roleId, CancellationToken cancellationToken);

    Task<Role?> FindByCodeAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> GetManyAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Permission>> ListPermissionsAsync(CancellationToken cancellationToken);

    void Add(Role role);

    void Remove(Role role);
}
