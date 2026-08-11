using Prumo.Domain.Authorization;
using Xunit;

namespace Prumo.Tests.Domain;

public class PermissionsTests
{
    [Fact]
    public void GetAllPermissions_ContainsFinancePermissions()
    {
        var all = Permissions.GetAllPermissions();
        Assert.Contains("finance.view",   all);
        Assert.Contains("finance.manage", all);
    }

    [Fact]
    public void GetAllPermissions_ContainsAccountsPayablePermissions()
    {
        var all = Permissions.GetAllPermissions();
        Assert.Contains("accounts_payable.view",              all);
        Assert.Contains("accounts_payable.create",            all);
        Assert.Contains("accounts_payable.edit",              all);
        Assert.Contains("accounts_payable.delete",            all);
        Assert.Contains("accounts_payable.manage_categories", all);
    }

    [Fact]
    public void AdminRolePermissions_ContainsAllPermissions()
    {
        var all   = Permissions.GetAllPermissions();
        var admin = Permissions.DefaultRolePermissions.Admin;
        foreach (var perm in all)
            Assert.Contains(perm, admin);
    }

    [Fact]
    public void RhRolePermissions_OnlyContainsHrPermissions()
    {
        var rh = Permissions.DefaultRolePermissions.RH;
        Assert.Contains("employees.view",   rh);
        Assert.Contains("worklogs.view",    rh);
        Assert.Contains("payments.view",    rh);
        Assert.DoesNotContain("finance.view",           rh);
        Assert.DoesNotContain("accounts_payable.view",  rh);
    }

    [Fact]
    public void FinanceiroRolePermissions_OnlyContainsFinancePermissions()
    {
        var fin = Permissions.DefaultRolePermissions.Financeiro;
        Assert.Contains("finance.view",   fin);
        Assert.Contains("finance.manage", fin);
        Assert.DoesNotContain("employees.view",          fin);
        Assert.DoesNotContain("accounts_payable.view",   fin);
    }

    [Fact]
    public void ContasAPagarRolePermissions_OnlyContainsApPermissions()
    {
        var ap = Permissions.DefaultRolePermissions.ContasAPagar;
        Assert.Contains("accounts_payable.view",   ap);
        Assert.Contains("accounts_payable.create", ap);
        Assert.DoesNotContain("finance.view",    ap);
        Assert.DoesNotContain("employees.view",  ap);
    }
}
