using FreshOrbit.Identity.Domain.Otp;
using FreshOrbit.Identity.Domain.Roles;
using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.Identity.Domain.Staff;
using FreshOrbit.Identity.Domain.Tokens;
using FreshOrbit.Identity.Domain.Users;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Identity.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(FreshOrbitDbContext db) : IUserRepository
{
    public Task<User?> GetAsync(Guid userId, CancellationToken cancellationToken)
        => db.Set<User>().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<User?> FindByPhoneAsync(string phone, CancellationToken cancellationToken)
        => db.Set<User>().FirstOrDefaultAsync(u => u.Phone == phone, cancellationToken);

    public Task<User?> FindStaffByEmailAsync(string email, CancellationToken cancellationToken)
        => db.Set<User>().FirstOrDefaultAsync(u => u.Email == email && u.UserType == UserType.Staff, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
        => db.Set<User>().AnyAsync(u => u.Email == email, cancellationToken);

    public void Add(User user) => db.Add(user);
}

internal sealed class StaffLoginFailureRepository(FreshOrbitDbContext db) : IStaffLoginFailureRepository
{
    public async Task<IReadOnlyList<DateTimeOffset>> GetFailureTimesAsync(string emailHash, string clientIp, DateTimeOffset since, CancellationToken cancellationToken)
        => await db.Set<StaffLoginFailure>().AsNoTracking()
            .Where(f => f.EmailHash == emailHash && f.ClientIp == clientIp && f.FailedAt > since)
            .OrderBy(f => f.FailedAt)
            .Select(f => f.FailedAt)
            .ToListAsync(cancellationToken);

    public void Add(StaffLoginFailure failure) => db.Add(failure);
}

internal sealed class OtpChallengeRepository(FreshOrbitDbContext db) : IOtpChallengeRepository
{
    public Task<OtpChallenge?> GetAsync(Guid challengeId, CancellationToken cancellationToken)
        => db.Set<OtpChallenge>().FirstOrDefaultAsync(c => c.Id == challengeId, cancellationToken);

    public async Task<IReadOnlyList<DateTimeOffset>> GetRequestTimesForPhoneAsync(string phone, DateTimeOffset since, CancellationToken cancellationToken)
        => await db.Set<OtpChallenge>().AsNoTracking()
            .Where(c => c.Phone == phone && c.RequestedAt > since)
            .OrderByDescending(c => c.RequestedAt)
            .Select(c => c.RequestedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DateTimeOffset>> GetRequestTimesForIpAsync(string ip, DateTimeOffset since, CancellationToken cancellationToken)
        => await db.Set<OtpChallenge>().AsNoTracking()
            .Where(c => c.RequestIp == ip && c.RequestedAt > since)
            .OrderByDescending(c => c.RequestedAt)
            .Select(c => c.RequestedAt)
            .ToListAsync(cancellationToken);

    public void Add(OtpChallenge challenge) => db.Add(challenge);
}

internal sealed class RefreshTokenRepository(FreshOrbitDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken)
        => db.Set<RefreshToken>().FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
        => await db.Set<RefreshToken>().Where(t => t.FamilyId == familyId && t.RevokedAt == null && t.ExpiresAt > now).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
        => await db.Set<RefreshToken>().Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now).ToListAsync(cancellationToken);

    public void Add(RefreshToken token) => db.Add(token);
}

internal sealed class StaffRepository(FreshOrbitDbContext db) : IStaffRepository
{
    public Task<StaffMember?> GetAsync(Guid staffMemberId, CancellationToken cancellationToken)
        => db.Set<StaffMember>().AsSplitQuery().FirstOrDefaultAsync(s => s.Id == staffMemberId, cancellationToken);

    public Task<StaffMember?> FindByUserAsync(Guid userId, CancellationToken cancellationToken)
        => db.Set<StaffMember>().AsSplitQuery().FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

    public Task<int> CountActiveWithRoleAsync(Guid roleId, Guid? exceptStaffMemberId, CancellationToken cancellationToken)
        => db.Set<StaffMember>().IgnoreAutoIncludes()
            .Where(s => s.IsActive && s.Id != exceptStaffMemberId && s.Roles.Any(r => r.RoleId == roleId))
            .CountAsync(cancellationToken);

    public Task<bool> AnyWithRoleAsync(Guid roleId, CancellationToken cancellationToken)
        => db.Set<StaffRole>().AnyAsync(r => r.RoleId == roleId, cancellationToken);

    public void Add(StaffMember staff) => db.Add(staff);
}

internal sealed class RoleRepository(FreshOrbitDbContext db) : IRoleRepository
{
    public Task<Role?> GetAsync(Guid roleId, CancellationToken cancellationToken)
        => db.Set<Role>().FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

    public Task<Role?> FindByCodeAsync(string code, CancellationToken cancellationToken)
        => db.Set<Role>().FirstOrDefaultAsync(r => r.Code == code, cancellationToken);

    public async Task<IReadOnlyList<Role>> GetManyAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
        => roleIds.Count == 0 ? [] : await db.Set<Role>().Where(r => roleIds.Contains(r.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken)
        => await db.Set<Role>().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Permission>> ListPermissionsAsync(CancellationToken cancellationToken)
        => await db.Set<Permission>().AsNoTracking().ToListAsync(cancellationToken);

    public void Add(Role role) => db.Add(role);

    public void Remove(Role role) => db.Remove(role);
}
