using System.Globalization;
using FreshOrbit.Identity.Application.Staff;
using FreshOrbit.Identity.Domain.Roles;
using FreshOrbit.Identity.Domain.Staff;
using FreshOrbit.Identity.Domain.Users;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Identity.Infrastructure.Persistence.Repositories;

internal sealed class StaffReads(FreshOrbitDbContext db) : IStaffReads
{
    public async Task<StaffListResponse> ListAsync(StaffListRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = from s in db.Set<StaffMember>().AsNoTracking()
                    join u in db.Set<User>().AsNoTracking() on s.UserId equals u.Id
                    select new { Staff = s, User = u };
        if (request.IsActive is { } isActive)
        {
            query = query.Where(x => x.Staff.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var pattern = $"%{request.Q.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Staff.FullName, pattern) || EF.Functions.ILike(x.User.Email!, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(x => x.Staff.FullName).ThenBy(x => x.Staff.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(cancellationToken);
        var roles = await RolesAsync(rows.SelectMany(r => r.Staff.RoleIds), cancellationToken);
        return new StaffListResponse([.. rows.Select(r => ToResponse(r.Staff, r.User, roles))], request.Page, request.PageSize, total);
    }

    public async Task<StaffResponse?> GetAsync(Guid staffMemberId, CancellationToken cancellationToken)
    {
        var row = await (from s in db.Set<StaffMember>().AsNoTracking()
                         join u in db.Set<User>().AsNoTracking() on s.UserId equals u.Id
                         where s.Id == staffMemberId
                         select new { Staff = s, User = u }).FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        return ToResponse(row.Staff, row.User, await RolesAsync(row.Staff.RoleIds, cancellationToken));
    }

    private async Task<Dictionary<Guid, Role>> RolesAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken)
    {
        var ids = roleIds.Distinct().ToList();
        return ids.Count == 0
            ? []
            : await db.Set<Role>().AsNoTracking().IgnoreAutoIncludes().Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, cancellationToken);
    }

    private static StaffResponse ToResponse(StaffMember staff, User user, Dictionary<Guid, Role> roles) => new(
        staff.Id,
        user.Id,
        user.Email,
        staff.FullName,
        [.. staff.RoleIds.Where(roles.ContainsKey).Select(id => roles[id]).OrderBy(r => r.Code, StringComparer.Ordinal).Select(r => new StaffRoleRef(r.Id, r.Code, r.Name))],
        staff.AllLocations,
        [.. staff.LocationIds],
        staff.IsActive,
        user.Status.ToCode(),
        user.LastLoginAt,
        staff.Version.ToString(CultureInfo.InvariantCulture));
}
