namespace BiomePampa.Domain.DTOs
{
    public class PermissionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
    }

    public class RolePermissionsDto
    {
        public string RoleName { get; set; } = null!;
        public List<PermissionDto> Permissions { get; set; } = new();
    }

    public class GrantPermissionDto
    {
        public string PermissionName { get; set; } = null!;
        public string? Reason { get; set; }
    }

    public class RevokePermissionDto
    {
        public string? Reason { get; set; }
    }

    public class PermissionAuditDto
    {
        public Guid Id { get; set; }
        public string RoleName { get; set; } = null!;
        public string PermissionName { get; set; } = null!;
        public string Action { get; set; } = null!;
        public string PerformedByUserEmail { get; set; } = null!;
        public DateTime PerformedAt { get; set; }
        public string? Reason { get; set; }
    }
}
