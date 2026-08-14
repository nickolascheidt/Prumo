using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prumo.Infrastructure.Migrations
{
    /// <summary>
    /// Backfill do catálogo padrão de Resources para tenants que já existiam.
    ///
    /// Até agora quem cobria esses tenants era o re-seed de startup
    /// (DbInitializer.EnsureTenantBootstrapAsync), removido porque ele ressuscitava
    /// grants revogados a cada restart. Esta migration faz o trabalho legítimo dele
    /// uma única vez: garante que todo tenant tenha os recursos do catálogo, sem
    /// tocar em nenhum ResourcePermission — quem concede/revoga acesso é o admin.
    ///
    /// Os valores abaixo são cópia de TenantBootstrapSeeder.DefaultResources. Se o
    /// catálogo mudar, é uma migration nova, não uma edição desta.
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
            // Intencionalmente vazio. Apagar os recursos removeria em cascata os
            // ResourcePermissions que os admins concederam depois — perder o trabalho
            // deles é pior do que esta migration ser irreversível.
        }
    }
}
