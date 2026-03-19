using BiomePampa.Domain.Enums;

namespace BiomePampa.Application.DTOs
{
    public class ResourceDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? Module { get; set; }
        public string? FrontendRoute { get; set; }
        public string? Icon { get; set; }
        public int DisplayOrder { get; set; }
        public PermissionLevel UserPermissionLevel { get; set; }
    }

    public class ResourcePermissionDto
    {
        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = null!;
        public Guid ResourceId { get; set; }
        public string ResourceCode { get; set; } = null!;
        public string ResourceName { get; set; } = null!;
        public PermissionLevel Level { get; set; }
    }

    public class UserPermissionsDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = null!;
        public string? FullName { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<ResourceDto> AllowedResources { get; set; } = new();
        public Dictionary<string, PermissionLevel> ResourcePermissions { get; set; } = new();
    }

    public class CreateResourceDto
    {
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? Module { get; set; }
        public string? FrontendRoute { get; set; }
        public string? Icon { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class UpdateResourceDto
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? Module { get; set; }
        public string? FrontendRoute { get; set; }
        public string? Icon { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class AssignResourcePermissionDto
    {
        public Guid RoleId { get; set; }
        public Guid ResourceId { get; set; }
        public PermissionLevel Level { get; set; }
    }
}
