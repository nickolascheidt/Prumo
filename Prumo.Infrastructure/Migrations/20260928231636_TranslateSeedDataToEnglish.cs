using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prumo.Infrastructure.Migrations
{
    /// <summary>
    /// Renames the canonical roles, the resource catalog and the seeded chart of accounts
    /// from Portuguese to English. Accounts are only renamed while they still carry the
    /// seeded name, so an account someone renamed is left alone.
    /// </summary>
    public partial class TranslateSeedDataToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""AspNetRoles"" SET ""Name"" = 'Administrator', ""NormalizedName"" = 'ADMINISTRATOR', ""Description"" = 'Full access to the system' WHERE ""TenantId"" IS NULL AND ""NormalizedName"" = 'ADMINISTRADOR';");
            migrationBuilder.Sql(@"UPDATE ""AspNetRoles"" SET ""Name"" = 'Employee', ""NormalizedName"" = 'EMPLOYEE', ""Description"" = 'Access for employees' WHERE ""TenantId"" IS NULL AND ""NormalizedName"" = 'FUNCIONARIO';");
            migrationBuilder.Sql(@"UPDATE ""AspNetRoles"" SET ""Name"" = 'Customer', ""NormalizedName"" = 'CUSTOMER', ""Description"" = 'Access for customers' WHERE ""TenantId"" IS NULL AND ""NormalizedName"" = 'CLIENTE';");
            migrationBuilder.Sql(@"UPDATE ""AspNetRoles"" SET ""Name"" = 'HR', ""NormalizedName"" = 'HR', ""Description"" = 'Access to the Human Resources module' WHERE ""TenantId"" IS NULL AND ""NormalizedName"" = 'RH';");
            migrationBuilder.Sql(@"UPDATE ""AspNetRoles"" SET ""Name"" = 'Finance', ""NormalizedName"" = 'FINANCE', ""Description"" = 'Access to the Finance module' WHERE ""TenantId"" IS NULL AND ""NormalizedName"" = 'FINANCEIRO';");
            migrationBuilder.Sql(@"UPDATE ""AspNetRoles"" SET ""Name"" = 'AccountsPayable', ""NormalizedName"" = 'ACCOUNTSPAYABLE', ""Description"" = 'Access to the Accounts Payable module' WHERE ""TenantId"" IS NULL AND ""NormalizedName"" = 'CONTASAPAGAR';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'User Management', ""Description"" = 'Create and manage users', ""Module"" = 'Administration', ""FrontendRoute"" = '/admin/members' WHERE ""Code"" = 'User.Management';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Role Management', ""Description"" = 'Create and manage roles', ""Module"" = 'Administration', ""FrontendRoute"" = '/admin/roles' WHERE ""Code"" = 'Role.Management';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Permission Management', ""Description"" = 'Configure permissions and access', ""Module"" = 'Administration', ""FrontendRoute"" = '/admin/permissions' WHERE ""Code"" = 'Permission.Management';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'System Settings', ""Description"" = 'General application settings', ""Module"" = 'Administration', ""FrontendRoute"" = '/admin/settings' WHERE ""Code"" = 'System.Configuration';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Main Dashboard', ""Description"" = 'Overview dashboard', ""Module"" = 'Dashboard', ""FrontendRoute"" = '/dashboard' WHERE ""Code"" = 'Dashboard.Main';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Accounting Dashboard', ""Description"" = 'Accounting indicators', ""Module"" = 'Dashboard', ""FrontendRoute"" = '/dashboard/accounting' WHERE ""Code"" = 'Dashboard.Accounting';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Finance Dashboard', ""Description"" = 'Finance indicators', ""Module"" = 'Dashboard', ""FrontendRoute"" = '/dashboard/finance' WHERE ""Code"" = 'Dashboard.Finance';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'HR Dashboard', ""Description"" = 'Human resources indicators', ""Module"" = 'Dashboard', ""FrontendRoute"" = '/dashboard/hr' WHERE ""Code"" = 'Dashboard.HR';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Admin Dashboard', ""Description"" = 'Administration indicators', ""Module"" = 'Dashboard', ""FrontendRoute"" = '/dashboard/admin' WHERE ""Code"" = 'Dashboard.Admin';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Chart of Accounts', ""Description"" = 'Create and manage the chart of accounts', ""Module"" = 'Finance', ""FrontendRoute"" = '/finance/chart-of-accounts' WHERE ""Code"" = 'ChartOfAccounts.Management';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'General Ledger', ""Description"" = 'View and manage general ledger journal entries', ""Module"" = 'Finance', ""FrontendRoute"" = '/finance/general-ledger' WHERE ""Code"" = 'GeneralLedger.Management';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Employees', ""Description"" = 'Employee management', ""Module"" = 'HR', ""FrontendRoute"" = '/hr/employees' WHERE ""Code"" = 'HR.Employees';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Work Logs', ""Description"" = 'Hours worked', ""Module"" = 'HR', ""FrontendRoute"" = '/hr/worklogs' WHERE ""Code"" = 'HR.WorkLogs';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'HR Payments', ""Description"" = 'Employee payments', ""Module"" = 'HR', ""FrontendRoute"" = '/hr/payments' WHERE ""Code"" = 'HR.Payments';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Payment Periods', ""Description"" = 'Periods generated for payment', ""Module"" = 'HR', ""FrontendRoute"" = '/hr/payment-periods' WHERE ""Code"" = 'HR.PaymentPeriods';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Accounts Payable', ""Description"" = 'Accounts payable entries', ""Module"" = 'AccountsPayable', ""FrontendRoute"" = '/accounts-payable' WHERE ""Code"" = 'AccountsPayable.Entries';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Assets' WHERE ""Code"" = '1' AND ""Name"" = 'Ativo';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Current Assets' WHERE ""Code"" = '1.1' AND ""Name"" = 'Ativo Circulante';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Cash and Cash Equivalents' WHERE ""Code"" = '1.1.1' AND ""Name"" = 'Caixa e Equivalentes';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Accounts Receivable' WHERE ""Code"" = '1.1.2' AND ""Name"" = 'Contas a Receber';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Non-current Assets' WHERE ""Code"" = '1.2' AND ""Name"" = 'Ativo Não Circulante';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Property, Plant and Equipment' WHERE ""Code"" = '1.2.1' AND ""Name"" = 'Imobilizado';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Liabilities' WHERE ""Code"" = '2' AND ""Name"" = 'Passivo';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Current Liabilities' WHERE ""Code"" = '2.1' AND ""Name"" = 'Passivo Circulante';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Suppliers / Accounts Payable' WHERE ""Code"" = '2.1.1' AND ""Name"" = 'Fornecedores / Contas a Pagar';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Loans and Borrowings' WHERE ""Code"" = '2.1.2' AND ""Name"" = 'Empréstimos e Financiamentos';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Salaries Payable' WHERE ""Code"" = '2.1.3' AND ""Name"" = 'Salários a Pagar';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Payroll Taxes Payable' WHERE ""Code"" = '2.1.4' AND ""Name"" = 'Encargos Sociais a Pagar';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Equity' WHERE ""Code"" = '3' AND ""Name"" = 'Patrimônio Líquido';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Share Capital' WHERE ""Code"" = '3.1' AND ""Name"" = 'Capital Social';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Retained Earnings' WHERE ""Code"" = '3.2' AND ""Name"" = 'Lucros/Prejuízos Acumulados';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Revenue' WHERE ""Code"" = '4' AND ""Name"" = 'Receita';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Operating Revenue' WHERE ""Code"" = '4.1' AND ""Name"" = 'Receita Operacional';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Expenses' WHERE ""Code"" = '5' AND ""Name"" = 'Despesas';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Operating Expenses' WHERE ""Code"" = '5.1' AND ""Name"" = 'Despesas Operacionais';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Personnel Expenses' WHERE ""Code"" = '5.1.1' AND ""Name"" = 'Despesas com Pessoal';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Administrative Expenses' WHERE ""Code"" = '5.1.2' AND ""Name"" = 'Despesas Administrativas';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Other Operating Expenses' WHERE ""Code"" = '5.1.3' AND ""Name"" = 'Despesas Operacionais';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Supplier Expenses' WHERE ""Code"" = '5.1.4' AND ""Name"" = 'Despesas com Fornecedores';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Cost of Goods and Services' WHERE ""Code"" = '5.2' AND ""Name"" = 'Custo dos Produtos/Serviços';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""AspNetRoles"" SET ""Name"" = 'Administrador', ""NormalizedName"" = 'ADMINISTRADOR', ""Description"" = 'Acesso total ao sistema' WHERE ""TenantId"" IS NULL AND ""NormalizedName"" = 'ADMINISTRATOR';");
            migrationBuilder.Sql(@"UPDATE ""AspNetRoles"" SET ""Name"" = 'Funcionario', ""NormalizedName"" = 'FUNCIONARIO', ""Description"" = 'Acesso para funcionários do sistema' WHERE ""TenantId"" IS NULL AND ""NormalizedName"" = 'EMPLOYEE';");
            migrationBuilder.Sql(@"UPDATE ""AspNetRoles"" SET ""Name"" = 'Cliente', ""NormalizedName"" = 'CLIENTE', ""Description"" = 'Acesso para clientes' WHERE ""TenantId"" IS NULL AND ""NormalizedName"" = 'CUSTOMER';");
            migrationBuilder.Sql(@"UPDATE ""AspNetRoles"" SET ""Name"" = 'RH', ""NormalizedName"" = 'RH', ""Description"" = 'Acesso ao módulo de Recursos Humanos' WHERE ""TenantId"" IS NULL AND ""NormalizedName"" = 'HR';");
            migrationBuilder.Sql(@"UPDATE ""AspNetRoles"" SET ""Name"" = 'Financeiro', ""NormalizedName"" = 'FINANCEIRO', ""Description"" = 'Acesso ao módulo Financeiro' WHERE ""TenantId"" IS NULL AND ""NormalizedName"" = 'FINANCE';");
            migrationBuilder.Sql(@"UPDATE ""AspNetRoles"" SET ""Name"" = 'ContasAPagar', ""NormalizedName"" = 'CONTASAPAGAR', ""Description"" = 'Acesso ao módulo de Contas a Pagar' WHERE ""TenantId"" IS NULL AND ""NormalizedName"" = 'ACCOUNTSPAYABLE';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Gestão de Usuários', ""Description"" = 'Cadastro e gerenciamento de usuários', ""Module"" = 'Administrativo', ""FrontendRoute"" = '/admin/usuarios' WHERE ""Code"" = 'User.Management';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Gestão de Roles', ""Description"" = 'Cadastro e gerenciamento de roles', ""Module"" = 'Administrativo', ""FrontendRoute"" = '/admin/roles' WHERE ""Code"" = 'Role.Management';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Gestão de Permissões', ""Description"" = 'Configuração de permissões e acessos', ""Module"" = 'Administrativo', ""FrontendRoute"" = '/admin/permissoes' WHERE ""Code"" = 'Permission.Management';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Configurações do Sistema', ""Description"" = 'Configurações gerais da aplicação', ""Module"" = 'Administrativo', ""FrontendRoute"" = '/admin/configuracoes' WHERE ""Code"" = 'System.Configuration';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Dashboard Principal', ""Description"" = 'Dashboard com visão geral', ""Module"" = 'Dashboard', ""FrontendRoute"" = '/dashboard' WHERE ""Code"" = 'Dashboard.Main';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Dashboard Contábil', ""Description"" = 'Painel com os indicadores da contabilidade', ""Module"" = 'Dashboard', ""FrontendRoute"" = '/dashboard/accounting' WHERE ""Code"" = 'Dashboard.Accounting';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Dashboard Financeiro', ""Description"" = 'Painel com os indicadores do financeiro', ""Module"" = 'Dashboard', ""FrontendRoute"" = '/dashboard/finance' WHERE ""Code"" = 'Dashboard.Finance';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Dashboard de RH', ""Description"" = 'Painel com os indicadores de recursos humanos', ""Module"" = 'Dashboard', ""FrontendRoute"" = '/dashboard/hr' WHERE ""Code"" = 'Dashboard.HR';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Dashboard Administrativo', ""Description"" = 'Painel com os indicadores de administração', ""Module"" = 'Dashboard', ""FrontendRoute"" = '/dashboard/admin' WHERE ""Code"" = 'Dashboard.Admin';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Plano de Contas', ""Description"" = 'Cadastro e gerenciamento do plano de contas', ""Module"" = 'Financeiro', ""FrontendRoute"" = '/finance/chart-of-accounts' WHERE ""Code"" = 'ChartOfAccounts.Management';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Razao Geral', ""Description"" = 'Consulta e gerenciamento de lancamentos contabeis do razao geral', ""Module"" = 'Financeiro', ""FrontendRoute"" = '/finance/general-ledger' WHERE ""Code"" = 'GeneralLedger.Management';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Funcionários', ""Description"" = 'Gestão de funcionários', ""Module"" = 'RH', ""FrontendRoute"" = '/hr/employees' WHERE ""Code"" = 'HR.Employees';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Horas Trabalhadas', ""Description"" = 'Registro de horas', ""Module"" = 'RH', ""FrontendRoute"" = '/hr/worklogs' WHERE ""Code"" = 'HR.WorkLogs';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Pagamentos RH', ""Description"" = 'Pagamentos de funcionários', ""Module"" = 'RH', ""FrontendRoute"" = '/hr/payments' WHERE ""Code"" = 'HR.Payments';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Períodos de Pagamento', ""Description"" = 'Períodos gerados para pagamento', ""Module"" = 'RH', ""FrontendRoute"" = '/hr/periodos' WHERE ""Code"" = 'HR.PaymentPeriods';");
            migrationBuilder.Sql(@"UPDATE ""Resources"" SET ""Name"" = 'Contas a Pagar', ""Description"" = 'Lançamentos de contas a pagar', ""Module"" = 'ContasAPagar', ""FrontendRoute"" = '/accounts-payable' WHERE ""Code"" = 'AccountsPayable.Entries';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Ativo' WHERE ""Code"" = '1' AND ""Name"" = 'Assets';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Ativo Circulante' WHERE ""Code"" = '1.1' AND ""Name"" = 'Current Assets';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Caixa e Equivalentes' WHERE ""Code"" = '1.1.1' AND ""Name"" = 'Cash and Cash Equivalents';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Contas a Receber' WHERE ""Code"" = '1.1.2' AND ""Name"" = 'Accounts Receivable';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Ativo Não Circulante' WHERE ""Code"" = '1.2' AND ""Name"" = 'Non-current Assets';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Imobilizado' WHERE ""Code"" = '1.2.1' AND ""Name"" = 'Property, Plant and Equipment';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Passivo' WHERE ""Code"" = '2' AND ""Name"" = 'Liabilities';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Passivo Circulante' WHERE ""Code"" = '2.1' AND ""Name"" = 'Current Liabilities';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Fornecedores / Contas a Pagar' WHERE ""Code"" = '2.1.1' AND ""Name"" = 'Suppliers / Accounts Payable';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Empréstimos e Financiamentos' WHERE ""Code"" = '2.1.2' AND ""Name"" = 'Loans and Borrowings';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Salários a Pagar' WHERE ""Code"" = '2.1.3' AND ""Name"" = 'Salaries Payable';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Encargos Sociais a Pagar' WHERE ""Code"" = '2.1.4' AND ""Name"" = 'Payroll Taxes Payable';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Patrimônio Líquido' WHERE ""Code"" = '3' AND ""Name"" = 'Equity';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Capital Social' WHERE ""Code"" = '3.1' AND ""Name"" = 'Share Capital';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Lucros/Prejuízos Acumulados' WHERE ""Code"" = '3.2' AND ""Name"" = 'Retained Earnings';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Receita' WHERE ""Code"" = '4' AND ""Name"" = 'Revenue';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Receita Operacional' WHERE ""Code"" = '4.1' AND ""Name"" = 'Operating Revenue';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Despesas' WHERE ""Code"" = '5' AND ""Name"" = 'Expenses';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Despesas Operacionais' WHERE ""Code"" = '5.1' AND ""Name"" = 'Operating Expenses';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Despesas com Pessoal' WHERE ""Code"" = '5.1.1' AND ""Name"" = 'Personnel Expenses';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Despesas Administrativas' WHERE ""Code"" = '5.1.2' AND ""Name"" = 'Administrative Expenses';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Despesas Operacionais' WHERE ""Code"" = '5.1.3' AND ""Name"" = 'Other Operating Expenses';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Despesas com Fornecedores' WHERE ""Code"" = '5.1.4' AND ""Name"" = 'Supplier Expenses';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Name"" = 'Custo dos Produtos/Serviços' WHERE ""Code"" = '5.2' AND ""Name"" = 'Cost of Goods and Services';");
        }
    }
}
