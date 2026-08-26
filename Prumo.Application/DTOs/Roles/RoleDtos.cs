namespace Prumo.Application.DTOs.Roles
{
    public record CreateTenantRoleDto(string Name, string? Description);

    /// <param name="IsCanonical">
    /// Role do sistema (<c>TenantId</c> nulo): visível em todo tenant, não pode ser
    /// excluída por ninguém.
    /// </param>
    /// <param name="MemberCount">Quantos membros <b>deste</b> tenant carregam a role.</param>
    public record TenantRoleDto(
        Guid Id,
        string Name,
        string? Description,
        bool IsCanonical,
        int MemberCount);
}
