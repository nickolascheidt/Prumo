using Prumo.Domain.Common;

namespace Prumo.Domain.Entities
{
    /// <summary>
    /// An application resource (screen, feature, module),
    /// e.g. "HR.Employees", "Dashboard.Finance".
    /// </summary>
    public class Resource : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }

        /// <summary>
        /// Unique resource identifier (e.g. "HR.Employees")
        /// </summary>
        public string Code { get; set; } = null!;

        /// <summary>
        /// Display name (e.g. "Employees")
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// Resource description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Module/area it belongs to (e.g. "HR", "Finance", "Administration")
        /// </summary>
        public string? Module { get; set; }

        /// <summary>
        /// Frontend route (optional)
        /// </summary>
        public string? FrontendRoute { get; set; }

        /// <summary>
        /// Menu icon (optional)
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Menu display order
        /// </summary>
        public int DisplayOrder { get; set; }

        // Relacionamentos
        public ICollection<ResourcePermission> ResourcePermissions { get; set; } = new List<ResourcePermission>();
    }
}
