using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;

namespace Prumo.Infrastructure.Authorization
{
    /// <summary>
    /// Valida unicidade de nome de role <b>por tenant</b>, e não globalmente.
    /// </summary>
    /// <remarks>
    /// O <see cref="RoleValidator{TRole}"/> padrão do Identity rejeita qualquer nome já
    /// existente, em qualquer lugar. Isso torna o índice
    /// <c>(NormalizedName, TenantId)</c> letra morta: o banco aceitaria a segunda
    /// "Leitura", mas o validador nunca deixa a escrita chegar lá.
    ///
    /// A regra aqui:
    /// <list type="bullet">
    ///   <item>role canônica (TenantId nulo) — nome único no sistema inteiro;</item>
    ///   <item>role de tenant — nome único <b>dentro daquele tenant</b>, e nunca igual
    ///         ao de uma canônica.</item>
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
                    Description = "O nome da role é obrigatório."
                });
            }

            var normalized = manager.NormalizeKey(name);

            // Uma canônica com este nome bloqueia todo mundo — inclusive um tenant que
            // tentasse criar a sua própria "RH".
            var clashesWithCanonical = await _db.Roles
                .AnyAsync(r => r.NormalizedName == normalized
                            && r.TenantId == null
                            && r.Id != role.Id);

            if (clashesWithCanonical)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "DuplicateRoleName",
                    Description = $"'{name}' é uma role do sistema e não pode ser recriada."
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
                        Description = $"Já existe uma role '{name}' neste tenant."
                    });
                }
            }

            return IdentityResult.Success;
        }
    }
}
