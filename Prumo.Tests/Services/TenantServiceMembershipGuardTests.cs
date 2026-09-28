using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Prumo.Application.DTOs.Tenants;
using Prumo.Application.Services;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;
using Prumo.Infrastructure.Services;

namespace Prumo.Tests.Services;

/// <summary>
/// The Owner position is only born with the tenant: no member insertion may create it,
/// otherwise an Admin manufactures a second Owner nobody can remove or demote. And leaving
/// the tenant has to take the feature roles along, otherwise they come back on their own
/// when the person is re-admitted.
/// </summary>
public class TenantServiceMembershipGuardTests
{
    private static ApplicationDbContext MakeDb(TenantContext tenantContext) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options, tenantContext);

    private static UserManager<ApplicationUser> MakeUserManager()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        return Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
    }

    private static TenantService MakeSut(ApplicationDbContext db, UserManager<ApplicationUser> userManager) =>
        new(db, userManager, Substitute.For<INotificationPublisher>(), Substitute.For<IConfiguration>());

    private static (ApplicationDbContext Db, TenantContext Ctx, Guid TenantId) MakeDbWithTenant(string slug = "acme")
    {
        var tenantContext = new TenantContext();
        var db = MakeDb(tenantContext);

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = slug, Slug = slug, OwnerUserId = Guid.NewGuid() });
        db.SaveChanges();

        tenantContext.SetTenant(tenantId);
        return (db, tenantContext, tenantId);
    }

    [Fact]
    public async Task Inviting_an_existing_account_as_Owner_is_refused()
    {
        var (db, _, tenantId) = MakeDbWithTenant();
        var userManager = MakeUserManager();
        var existing = new ApplicationUser { Id = Guid.NewGuid(), Email = "z@example.com" };
        userManager.FindByEmailAsync("z@example.com").Returns(existing);
        var sut = MakeSut(db, userManager);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.InviteMemberAsync(tenantId,
                new InviteMemberRequestDto("z@example.com", TenantRole.Owner), Guid.NewGuid()));

        Assert.False(await db.TenantUsers.AnyAsync(tu => tu.UserId == existing.Id));
    }

    [Fact]
    public async Task Inviting_an_address_without_account_as_Owner_is_refused()
    {
        var (db, _, tenantId) = MakeDbWithTenant();
        var userManager = MakeUserManager();
        userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        var sut = MakeSut(db, userManager);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.InviteMemberAsync(tenantId,
                new InviteMemberRequestDto("new@example.com", TenantRole.Owner), Guid.NewGuid()));

        Assert.False(await db.TenantInvitations.AnyAsync());
    }

    [Fact]
    public async Task Adding_a_member_as_Owner_is_refused()
    {
        var (db, _, tenantId) = MakeDbWithTenant();
        var sut = MakeSut(db, MakeUserManager());
        var userId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.AddMemberAsync(tenantId, userId, TenantRole.Owner));

        Assert.False(await db.TenantUsers.AnyAsync(tu => tu.UserId == userId));
    }

    [Fact]
    public async Task A_legacy_pending_invitation_as_Owner_admits_the_person_as_Member()
    {
        // An invitation stored before this rule existed. Honoring the position would reopen
        // the hole; refusing would leave the person out of a tenant that expected them. They
        // join, but as Member.
        var (db, _, tenantId) = MakeDbWithTenant();
        db.TenantInvitations.Add(new TenantInvitation
        {
            TenantId = tenantId,
            Email = "legacy@example.com",
            NormalizedEmail = "LEGACY@EXAMPLE.COM",
            Role = TenantRole.Owner,
            InvitedByUserId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
        var sut = MakeSut(db, MakeUserManager());
        var newUserId = Guid.NewGuid();

        await sut.AcceptPendingInvitationsAsync(newUserId, "legacy@example.com");

        var membership = await db.TenantUsers.SingleAsync(tu => tu.UserId == newUserId);
        Assert.Equal(TenantRole.Member, membership.Role);
    }

    [Fact]
    public async Task Removing_a_member_revokes_their_feature_roles_in_that_tenant()
    {
        var (db, _, tenantId) = MakeDbWithTenant();
        var userId = Guid.NewGuid();
        db.TenantUsers.Add(new TenantUser { TenantId = tenantId, UserId = userId, Role = TenantRole.Member });
        db.TenantUserRoles.Add(new TenantUserRole { TenantId = tenantId, UserId = userId, RoleId = Guid.NewGuid() });
        await db.SaveChangesAsync();
        var sut = MakeSut(db, MakeUserManager());

        await sut.RemoveMemberAsync(tenantId, userId);

        Assert.False(await db.TenantUserRoles.IgnoreQueryFilters()
            .AnyAsync(t => t.TenantId == tenantId && t.UserId == userId));
    }

    [Fact]
    public async Task Removing_a_member_keeps_their_feature_roles_in_other_tenants()
    {
        var (db, _, tenantId) = MakeDbWithTenant();
        var otherTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.TenantUsers.Add(new TenantUser { TenantId = tenantId, UserId = userId, Role = TenantRole.Member });
        db.TenantUserRoles.Add(new TenantUserRole { TenantId = tenantId, UserId = userId, RoleId = Guid.NewGuid() });
        db.TenantUserRoles.Add(new TenantUserRole { TenantId = otherTenantId, UserId = userId, RoleId = Guid.NewGuid() });
        await db.SaveChangesAsync();
        var sut = MakeSut(db, MakeUserManager());

        await sut.RemoveMemberAsync(tenantId, userId);

        Assert.True(await db.TenantUserRoles.IgnoreQueryFilters()
            .AnyAsync(t => t.TenantId == otherTenantId && t.UserId == userId));
    }

    [Fact]
    public async Task Member_list_shows_feature_roles_even_when_the_token_points_at_another_tenant()
    {
        // TenantsController does not use [TenantModule], so the TenantContext comes from the
        // claim and may be another of the same user's tenants. The roles subquery must not
        // depend on it.
        var (db, ctx, tenantId) = MakeDbWithTenant();
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.Roles.Add(new ApplicationRole { Id = roleId, Name = "HR", NormalizedName = "HR" });
        db.Users.Add(new ApplicationUser { Id = userId, Email = "m@example.com", UserName = "m@example.com" });
        db.TenantUsers.Add(new TenantUser { TenantId = tenantId, UserId = userId, Role = TenantRole.Member });
        db.TenantUserRoles.Add(new TenantUserRole { TenantId = tenantId, UserId = userId, RoleId = roleId });
        await db.SaveChangesAsync();

        ctx.SetTenant(Guid.NewGuid());
        var sut = MakeSut(db, MakeUserManager());

        var members = await sut.GetMembersAsync(tenantId);

        var member = Assert.Single(members);
        Assert.Equal(new[] { "HR" }, member.Roles);
    }
}
