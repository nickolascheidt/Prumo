using Microsoft.AspNetCore.Identity;

namespace SaaS_BasePlatform.Domain.Entities
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string? FullName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
