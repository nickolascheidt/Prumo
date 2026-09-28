namespace Prumo.Domain.Authorization
{
    /// <summary>
    /// Canonical role names the system seeds.
    /// </summary>
    /// <remarks>
    /// Access control does not live here: it is <c>ResourcePermission</c> (role × resource →
    /// level), checked by <c>[TenantModule]</c> on every request.
    /// </remarks>
    public static class Permissions
    {
        /// <summary>
        /// Canonical Identity role names. <see cref="MasterAdmin"/> is global (cross-tenant);
        /// the feature roles are assigned per tenant via TenantUserRole.
        /// </summary>
        public static class Roles
        {
            public const string MasterAdmin = "Administrator";
            public const string Employee = "Employee";
            public const string Customer = "Customer";
            public const string HR = "HR";
            public const string Finance = "Finance";
            public const string AccountsPayable = "AccountsPayable";

            /// <summary>Roles a tenant admin may assign to members within their tenant.</summary>
            public static readonly IReadOnlyList<string> AssignableFeatureRoles = new[]
            {
                Employee, Customer, HR, Finance, AccountsPayable
            };

            /// <summary>
            /// Every canonical role, master admin included. Exists so the frontend never
            /// hard-codes role names.
            /// </summary>
            public static readonly IReadOnlyList<string> All = new[]
            {
                MasterAdmin, Employee, Customer, HR, Finance, AccountsPayable
            };
        }

    }
}
