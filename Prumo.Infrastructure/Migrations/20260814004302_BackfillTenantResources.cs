using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prumo.Infrastructure.Migrations
{
    /// <summary>
    /// Backfills the default Resource catalog for tenants that already existed.
    ///
    /// Until then those tenants were covered by a startup re-seed, removed because it
    /// brought revoked grants back on every restart. This migration does its legitimate
    /// work exactly once: it makes sure every tenant has the catalog resources, without
    /// touching any ResourcePermission — granting and revoking access is the admin's job.
    ///
    /// The values below are a copy of TenantBootstrapSeeder.DefaultResources at the time.
    /// If the catalog changes, that is a new migration, not an edit to this one.
    /// </summary>
    public partial class BackfillTenantResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO ""Resources""
                    (""Id"", ""TenantId"", ""Code"", ""Name"", ""Description"", ""Module"", ""FrontendRoute"", ""Icon"", ""DisplayOrder"", ""IsActive"", ""CreatedAt"")
                SELECT gen_random_uuid(), t.""Id"", c.code, c.name, c.description, c.module, c.route, c.icon, c.display_order, TRUE, NOW() AT TIME ZONE 'utc'
                FROM ""Tenants"" t
                CROSS JOIN (VALUES
                    ('User.Management',            'Gestão de Usuários',        'Cadastro e gerenciamento de usuários',                          'Administrativo', '/admin/usuarios',              'manage_accounts',       20),
                    ('Role.Management',            'Gestão de Roles',           'Cadastro e gerenciamento de roles',                             'Administrativo', '/admin/roles',                 'admin_panel_settings',  21),
                    ('Permission.Management',      'Gestão de Permissões',      'Configuração de permissões e acessos',                          'Administrativo', '/admin/permissoes',            'security',              22),
                    ('System.Configuration',       'Configurações do Sistema',  'Configurações gerais da aplicação',                             'Administrativo', '/admin/configuracoes',         'settings',              23),
                    ('Dashboard.Main',             'Dashboard Principal',       'Dashboard com visão geral',                                     'Dashboard',      '/dashboard',                   'dashboard',              0),
                    ('ChartOfAccounts.Management', 'Plano de Contas',           'Cadastro e gerenciamento do plano de contas',                   'Financeiro',     '/finance/chart-of-accounts',   'account_tree',          29),
                    ('GeneralLedger.Management',   'Razao Geral',               'Consulta e gerenciamento de lancamentos contabeis do razao geral','Financeiro',    '/finance/general-ledger',      'menu_book',             30),
                    ('HR.Employees',               'Funcionários',              'Gestão de funcionários',                                        'RH',             '/hr/employees',                'badge',                 40),
                    ('HR.WorkLogs',                'Horas Trabalhadas',         'Registro de horas',                                             'RH',             '/hr/worklogs',                 'schedule',              41),
                    ('HR.Payments',                'Pagamentos RH',             'Pagamentos de funcionários',                                    'RH',             '/hr/payments',                 'payments',              42),
                    ('HR.PaymentPeriods',          'Períodos de Pagamento',     'Períodos gerados para pagamento',                               'RH',             '/hr/periodos',                 'event_note',            43),
                    ('AccountsPayable.Entries',    'Contas a Pagar',            'Lançamentos de contas a pagar',                                 'ContasAPagar',   '/accounts-payable',            'request_quote',         50)
                ) AS c(code, name, description, module, route, icon, display_order)
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""Resources"" r
                    WHERE r.""TenantId"" = t.""Id"" AND r.""Code"" = c.code
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty. Deleting the resources would cascade to the
            // ResourcePermissions admins granted afterwards — losing their work is worse
            // than this migration being irreversible.
        }
    }
}
