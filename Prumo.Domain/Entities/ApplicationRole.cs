using Microsoft.AspNetCore.Identity;

namespace Prumo.Domain.Entities
{
    public class ApplicationRole : IdentityRole<Guid>
    {
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tenant that owns this role. <c>null</c> = canonical system role (Administrator,
        /// HR, Finance, AccountsPayable, Employee, Customer), visible in every tenant.
        /// Set = created by that tenant and visible only there.
        /// </summary>
        /// <remarks>
        /// Deliberately does <b>not</b> implement <c>ITenantScoped</c>: the global filter is
        /// fail-closed and would hide the canonical roles (null TenantId) from everyone,
        /// login included. Tenant filtering is explicit in the listings.
        /// </remarks>
        public Guid? TenantId { get; set; }
    }
}
