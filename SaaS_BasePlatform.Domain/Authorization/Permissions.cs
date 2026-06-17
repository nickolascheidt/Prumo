namespace SaaS_BasePlatform.Domain.Authorization
{
    /// <summary>
    /// Catálogo canônico de permissões do sistema.
    /// Permissões seguem o padrão: [módulo].[ação]
    /// Suporta wildcards: [módulo].* concede todas as permissões do módulo
    /// </summary>
    public static class Permissions
    {
        public static class Employees
        {
            public const string All = "employees.*";
            public const string View = "employees.view";
            public const string Create = "employees.create";
            public const string Edit = "employees.edit";
            public const string Delete = "employees.delete";
            public const string ManagePayments = "employees.manage_payments";
        }

        public static class WorkLogs
        {
            public const string All = "worklogs.*";
            public const string View = "worklogs.view";
            public const string Create = "worklogs.create";
            public const string Edit = "worklogs.edit";
            public const string Delete = "worklogs.delete";
        }

        public static class Payments
        {
            public const string All = "payments.*";
            public const string View = "payments.view";
            public const string Create = "payments.create";
            public const string Delete = "payments.delete";
            public const string ViewReports = "payments.view_reports";
        }

        public static class Products
        {
            public const string All = "products.*";
            public const string View = "products.view";
            public const string Create = "products.create";
            public const string Edit = "products.edit";
            public const string Delete = "products.delete";
        }

        public static class Customers
        {
            public const string All = "customers.*";
            public const string View = "customers.view";
            public const string Create = "customers.create";
            public const string Edit = "customers.edit";
            public const string Delete = "customers.delete";
        }

        public static class Stock
        {
            public const string All = "stock.*";
            public const string View = "stock.view";
            public const string Manage = "stock.manage";
            public const string ViewReports = "stock.view_reports";
        }

        public static class Finance
        {
            public const string All    = "finance.*";
            public const string View   = "finance.view";
            public const string Manage = "finance.manage";
        }

        public static class AccountsPayable
        {
            public const string All              = "accounts_payable.*";
            public const string View             = "accounts_payable.view";
            public const string Create           = "accounts_payable.create";
            public const string Edit             = "accounts_payable.edit";
            public const string Delete           = "accounts_payable.delete";
            public const string ManageCategories = "accounts_payable.manage_categories";
        }

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
        }

        /// <summary>
        /// Retorna todas as permissões do sistema
        /// </summary>
        public static IReadOnlyCollection<string> GetAllPermissions()
        {
            return new[]
            {
                // Employees
                Employees.View,
                Employees.Create,
                Employees.Edit,
                Employees.Delete,
                Employees.ManagePayments,

                // WorkLogs
                WorkLogs.View,
                WorkLogs.Create,
                WorkLogs.Edit,
                WorkLogs.Delete,

                // Payments
                Payments.View,
                Payments.Create,
                Payments.Delete,
                Payments.ViewReports,

                // Products
                Products.View,
                Products.Create,
                Products.Edit,
                Products.Delete,

                // Customers
                Customers.View,
                Customers.Create,
                Customers.Edit,
                Customers.Delete,

                // Stock
                Stock.View,
                Stock.Manage,
                Stock.ViewReports,

                // Finance
                Finance.View,
                Finance.Manage,

                // AccountsPayable
                AccountsPayable.View,
                AccountsPayable.Create,
                AccountsPayable.Edit,
                AccountsPayable.Delete,
                AccountsPayable.ManageCategories,
            };
        }

        /// <summary>
        /// Verifica se uma permissão hierárquica (com wildcard) cobre a permissão requerida
        /// Ex: "employees.*" cobre "employees.view", "employees.create", etc.
        /// </summary>
        public static bool MatchesWildcard(string userPermission, string requiredPermission)
        {
            if (userPermission == requiredPermission)
                return true;

            if (!userPermission.EndsWith(".*"))
                return false;

            var module = userPermission[..^2]; // Remove ".*"
            return requiredPermission.StartsWith(module + ".");
        }

        /// <summary>
        /// Verifica se o usuário possui a permissão requerida (considerando wildcards)
        /// </summary>
        public static bool HasPermission(IEnumerable<string> userPermissions, string requiredPermission)
        {
            return userPermissions.Any(up => MatchesWildcard(up, requiredPermission));
        }

        /// <summary>
        /// Retorna permissões padrão para cada role
        /// </summary>
        public static class DefaultRolePermissions
        {
            public static IReadOnlyCollection<string> Admin => GetAllPermissions();

            public static IReadOnlyCollection<string> Funcionario => new[]
            {
                // Employees - apenas visualizar
                Employees.View,
                
                // WorkLogs - gerenciar próprios registros
                WorkLogs.View,
                WorkLogs.Create,
                WorkLogs.Edit,
                
                // Products - visualizar
                Products.View,
                
                // Customers - gerenciar
                Customers.View,
                Customers.Create,
                Customers.Edit,
                
                // Stock - visualizar
                Stock.View
            };

            public static IReadOnlyCollection<string> Cliente => new[]
            {
                // Apenas visualizar produtos
                Products.View
            };

            public static IReadOnlyCollection<string> RH => new[]
            {
                Employees.View, Employees.Create, Employees.Edit, Employees.Delete, Employees.ManagePayments,
                WorkLogs.View,  WorkLogs.Create,  WorkLogs.Edit,  WorkLogs.Delete,
                Payments.View,  Payments.Create,  Payments.Delete, Payments.ViewReports
            };

            public static IReadOnlyCollection<string> Financeiro => new[]
            {
                Finance.View, Finance.Manage
            };

            public static IReadOnlyCollection<string> ContasAPagar => new[]
            {
                AccountsPayable.View,   AccountsPayable.Create, AccountsPayable.Edit,
                AccountsPayable.Delete, AccountsPayable.ManageCategories
            };
        }
    }
}
