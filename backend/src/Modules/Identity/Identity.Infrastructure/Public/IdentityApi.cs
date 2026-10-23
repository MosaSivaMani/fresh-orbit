using FreshOrbit.Identity.Application.Auth;
using FreshOrbit.Identity.Domain.Roles;
using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.Identity.Domain.Staff;
using FreshOrbit.Identity.Domain.Tokens;
using FreshOrbit.Identity.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using DomainUser = FreshOrbit.Identity.Domain.Users.User;
using DomainUserStatus = FreshOrbit.Identity.Domain.Users.UserStatus;
using DomainUserType = FreshOrbit.Identity.Domain.Users.UserType;

namespace FreshOrbit.Identity.Infrastructure.Public;

internal sealed class IdentityApi(FreshOrbitDbContext db, ICustomerAccountDirectory customers, SessionRevoker revoker, IClock clock) : IIdentityApi
{
    public async Task<UserSummary?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var users = await GetUsersAsync([userId], cancellationToken);
        return users.Count == 0 ? null : users[0];
    }

    public async Task<IReadOnlyList<UserSummary>> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0)
        {
            return [];
        }

        var users = await db.Set<DomainUser>().AsNoTracking().Where(u => userIds.Contains(u.Id)).ToListAsync(cancellationToken);
        var customerIds = users.Where(u => u.UserType == DomainUserType.Customer).Select(u => u.Id).ToList();
        var customerEmails = customerIds.Count == 0 ? new Dictionary<Guid, string>() : await customers.FindEmailsAsync(customerIds, cancellationToken);
        return [.. users.Select(u => new UserSummary(
            u.Id,
            u.Phone,
            u.Email ?? customerEmails.GetValueOrDefault(u.Id),
            u.UserType == DomainUserType.Staff ? UserType.Staff : UserType.Customer)
        {
            Status = u.Status switch
            {
                DomainUserStatus.Locked => UserStatus.Locked,
                DomainUserStatus.Disabled => UserStatus.Disabled,
                _ => UserStatus.Active,
            },
        })];
    }

    public Task<Guid?> FindUserIdByPhoneAsync(string phone, CancellationToken cancellationToken = default)
        => db.Set<DomainUser>().AsNoTracking().Where(u => u.Phone == phone).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<StaffSummary>> GetStaffMembersAsync(IReadOnlyCollection<Guid> staffMemberIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(staffMemberIds);
        if (staffMemberIds.Count == 0)
        {
            return [];
        }

        return await (from s in db.Set<StaffMember>().AsNoTracking().IgnoreAutoIncludes()
                      join u in db.Set<DomainUser>().AsNoTracking() on s.UserId equals u.Id
                      where staffMemberIds.Contains(s.Id)
                      select new StaffSummary(s.Id, u.Id, s.FullName, u.Email, s.IsActive)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetStaffUserIdsWithPermissionAsync(string permission, Guid? fulfillmentLocationId, CancellationToken cancellationToken = default)
    {
        var roleIds = db.Set<Role>().AsNoTracking().IgnoreAutoIncludes()
            .Where(r => r.Code == IdentityErrors.AdminRoleCode || r.Permissions.Any(p => p.PermissionCode == permission))
            .Select(r => r.Id);
        var query = from s in db.Set<StaffMember>().AsNoTracking().IgnoreAutoIncludes()
                    join u in db.Set<DomainUser>().AsNoTracking() on s.UserId equals u.Id
                    where s.IsActive && u.Status == DomainUserStatus.Active && s.Roles.Any(r => roleIds.Contains(r.RoleId))
                    select s;
        if (fulfillmentLocationId is { } locationId)
        {
            query = query.Where(s => s.AllLocations || s.Locations.Any(l => l.FulfillmentLocationId == locationId));
        }

        return await query.Select(s => s.UserId).Distinct().ToListAsync(cancellationToken);
    }

    public async Task SetUserBlockedAsync(Guid userId, bool blocked, CancellationToken cancellationToken = default)
    {
        var user = await db.Set<DomainUser>().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException($"User {userId} does not exist.");
        if (blocked)
        {
            user.Disable();
            await revoker.RevokeAllAsync(userId, RevokeReason.SessionsRevoked, cancellationToken);
        }
        else
        {
            user.Enable();
        }
    }

    public async Task AnonymiseUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Set<DomainUser>().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException($"User {userId} does not exist.");
        user.Anonymise(clock.UtcNow);
        await revoker.RevokeAllAsync(userId, RevokeReason.SessionsRevoked, cancellationToken);
    }
}
