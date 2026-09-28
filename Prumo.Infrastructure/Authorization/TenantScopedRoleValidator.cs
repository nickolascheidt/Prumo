using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;

namespace Prumo.Infrastructure.Authorization
{
    /// <summary>
    /// Validates role name uniqueness <b>per tenant</b>, not globally.
    /// </summary>
    /// <remarks>
    /// Identity's default <see cref="RoleValidator{TRole}"/> rejects any name that already
    /// exists anywhere. That makes the <c>(NormalizedName, TenantId)</c> index a dead
    /// letter: the database would accept a second "Viewer", but the validator never lets
    /// the write get there.
    ///
    /// The rule here:
    /// <list type="bullet">
    ///   <item>canonical role (null TenantId) — name unique across the whole system;</item>
    ///   <item>tenant role — name unique <b>within that tenant</b>, and never equal to a
    ///         canonical one.</item>
    /// </list>
    /// </remarks>
    public class TenantScopedRoleValidator : IRoleValidator<ApplicationRole>
    {
        private readonly ApplicationDbContext _db;

        public TenantScopedRoleValidator(ApplicationDbContext db) => _db = db;

        public async Task<IdentityResult> ValidateAsync(
            RoleManager<ApplicationRole> manager, ApplicationRole role)
        {
            var name = role.Name;

            if (string.IsNullOrWhiteSpace(name))
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "InvalidRoleName",
                    Description = "The role name is required."
                });
            }

            var normalized = manager.NormalizeKey(name);

            // A canonical role with this name blocks everyone — including a tenant trying
            // to create its own "HR".
            var clashesWithCanonical = await _db.Roles
                .AnyAsync(r => r.NormalizedName == normalized
                            && r.TenantId == null
                            && r.Id != role.Id);

            if (clashesWithCanonical)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "DuplicateRoleName",
                    Description = $"'{name}' is a system role and cannot be recreated."
                });
            }

            if (role.TenantId is not null)
            {
                var clashesInSameTenant = await _db.Roles
                    .AnyAsync(r => r.NormalizedName == normalized
                                && r.TenantId == role.TenantId
                                && r.Id != role.Id);

                if (clashesInSameTenant)
                {
                    return IdentityResult.Failed(new IdentityError
                    {
                        Code = "DuplicateRoleName",
                        Description = $"A role named '{name}' already exists in this tenant."
                    });
                }
            }

            return IdentityResult.Success;
        }
    }
}
