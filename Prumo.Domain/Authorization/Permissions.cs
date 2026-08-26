namespace Prumo.Domain.Authorization
{
    /// <summary>
    /// Nomes canônicos de role do sistema.
    /// </summary>
    /// <remarks>
    /// Esta classe já foi o catálogo de permissões em string (<c>employees.view</c> e
    /// afins). Todo esse catálogo foi aposentado no item 3B: nenhum endpoint o consultava
    /// — zero <c>[Authorize(Policy=…)]</c> — e o frontend nunca lia os claims que ele
    /// produzia. Quem controla acesso é <c>ResourcePermission</c> (role × recurso →
    /// nível), checada por <c>[TenantModule]</c> a cada request.
    ///
    /// O que sobrou aqui são os nomes das roles que o sistema semeia, que continuam
    /// valendo.
    /// </remarks>
    public static class Permissions
    {
        /// <summary>
        /// Canonical Identity role names. <see cref="Roles.MasterAdmin"/> is global (cross-tenant);
        /// the feature roles are assigned per-tenant via TenantUserRole.
        /// </summary>
        public static class Roles
        {
            public const string MasterAdmin = "Administrador";
            public const string Funcionario = "Funcionario";
            public const string Cliente = "Cliente";
            public const string RH = "RH";
            public const string Financeiro = "Financeiro";
            public const string ContasAPagar = "ContasAPagar";

            /// <summary>Roles a tenant admin may assign to members within their tenant.</summary>
            public static readonly IReadOnlyList<string> AssignableFeatureRoles = new[]
            {
                Funcionario, Cliente, RH, Financeiro, ContasAPagar
            };

            /// <summary>
            /// Toda role canônica, incluindo o master admin. É a lista que a tela de
            /// "Permissões por Role" configura — o master admin entra porque as
            /// RolePermissions dele são reais e valem para os endpoints gateados por
            /// policy. Existe para o frontend não chumbar nomes de role; foi assim que
            /// a role fantasma "Usuario" sobreviveu por meses.
            /// </summary>
            public static readonly IReadOnlyList<string> All = new[]
            {
                MasterAdmin, Funcionario, Cliente, RH, Financeiro, ContasAPagar
            };
        }

    }
}
