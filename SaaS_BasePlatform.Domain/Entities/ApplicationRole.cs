using Microsoft.AspNetCore.Identity;

namespace SaaS_BasePlatform.Domain.Entities
{
    public class ApplicationRole : IdentityRole<Guid>
    {
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
