using Prumo.Domain.Common;
using Prumo.Domain.Enums;

namespace Prumo.Domain.Entities
{
    /// <summary>
    /// An e-mail the admin added to the tenant that does not have an account yet.
    ///
    /// **It is not a link with a token.** The invitation authorizes nothing: it is a note
    /// that this address, once it exists, joins this tenant with this position. Whoever
    /// signs up with the invited address is admitted because they proved they own the
    /// mailbox — the same proof e-mail confirmation requires. That is what lets the e-mail
    /// notice be best-effort: if it never arrives, nobody joins a tenant by mistake.
    /// </summary>
    public class TenantInvitation : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        /// <summary>
        /// Stored **normalized** (upper case), as Identity does with `NormalizedEmail` —
        /// otherwise "Ana@x.com" and "ana@x.com" become different invitations.
        /// </summary>
        public string NormalizedEmail { get; set; } = string.Empty;

        /// <summary>The address as the admin typed it, for display.</summary>
        public string Email { get; set; } = string.Empty;

        public TenantRole Role { get; set; } = TenantRole.Member;

        public Guid InvitedByUserId { get; set; }

        /// <summary>Null while pending. Set when someone signs up with the address.</summary>
        public DateTime? AcceptedAt { get; set; }
    }
}
