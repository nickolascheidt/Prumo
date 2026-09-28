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
/// Admins add people by invitation instead of creating their account with a password they
/// typed, so nobody's initial password passes through the administrator.
/// </summary>
public class TenantServiceInvitationTests
{
    /// <summary>
    /// `TenantInvitation` is `ITenantScoped`, so the fail-closed global filter applies to it.
    /// Without an `ITenantContext`, any filtered query throws — the context comes along, and
    /// the tests point it at the seeded tenant.
    /// </summary>
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

    private static TenantService MakeSut(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        INotificationPublisher? notifications = null) =>
        new(db,
            userManager,
            notifications ?? Substitute.For<INotificationPublisher>(),
            Substitute.For<IConfiguration>());

    /// <summary>Creates the database already pointing at a new tenant, and returns both.</summary>
    private static (ApplicationDbContext Db, Guid TenantId) MakeDbWithTenant(string name = "Acme")
    {
        var tenantContext = new TenantContext();
        var db = MakeDb(tenantContext);

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = name, Slug = "acme", OwnerUserId = Guid.NewGuid() });
        db.SaveChanges();

        tenantContext.SetTenant(tenantId);
        return (db, tenantId);
    }

    [Fact]
    public async Task Inviting_an_address_that_already_has_an_account_makes_it_a_member_now()
    {
        var (db, tenantId) = MakeDbWithTenant();
        var userManager = MakeUserManager();

        var existing = new ApplicationUser { Id = Guid.NewGuid(), Email = "ana@example.com" };
        userManager.FindByEmailAsync("ana@example.com").Returns(existing);

        var sut = MakeSut(db, userManager);

        var result = await sut.InviteMemberAsync(
            tenantId, new InviteMemberRequestDto("ana@example.com", TenantRole.Member), Guid.NewGuid());

        Assert.True(result.JoinedImmediately);
        Assert.True(await db.TenantUsers.AnyAsync(tu => tu.TenantId == tenantId && tu.UserId == existing.Id));
        Assert.False(await db.TenantInvitations.AnyAsync());
    }

    [Fact]
    public async Task Inviting_an_address_without_an_account_leaves_a_pending_invitation()
    {
        var (db, tenantId) = MakeDbWithTenant();
        var userManager = MakeUserManager();
        userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);

        var notifications = Substitute.For<INotificationPublisher>();
        var sut = MakeSut(db, userManager, notifications);

        var result = await sut.InviteMemberAsync(
            tenantId, new InviteMemberRequestDto("new@example.com", TenantRole.Admin), Guid.NewGuid());

        Assert.False(result.JoinedImmediately);

        var invitation = await db.TenantInvitations.SingleAsync();
        Assert.Equal(TenantRole.Admin, invitation.Role);
        Assert.Null(invitation.AcceptedAt);

        // Stored normalized, otherwise "New@example.com" would become a second invitation.
        Assert.Equal("NEW@EXAMPLE.COM", invitation.NormalizedEmail);

        await notifications.Received(1).PublishAsync(
            Arg.Is<NotificationMessage>(m => m.Type == NotificationTypes.TenantInvitation),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Signing_up_with_an_invited_address_joins_the_tenant()
    {
        var (db, tenantId) = MakeDbWithTenant();
        var userManager = MakeUserManager();

        db.TenantInvitations.Add(new TenantInvitation
        {
            TenantId = tenantId,
            Email = "Invited@Example.com",
            NormalizedEmail = "INVITED@EXAMPLE.COM",
            Role = TenantRole.Member,
            InvitedByUserId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var sut = MakeSut(db, userManager);
        var newUserId = Guid.NewGuid();

        // Matches with a different case from what the admin typed — the common case, and
        // without normalizing on both sides the person would sign up and join nothing.
        var joined = await sut.AcceptPendingInvitationsAsync(newUserId, "invited@example.com");

        Assert.Equal(1, joined);
        Assert.True(await db.TenantUsers.AnyAsync(tu => tu.TenantId == tenantId && tu.UserId == newUserId));
        Assert.NotNull((await db.TenantInvitations.SingleAsync()).AcceptedAt);
    }

    [Fact]
    public async Task Signing_up_with_an_address_nobody_invited_joins_nothing()
    {
        var (db, tenantId) = MakeDbWithTenant();
        var userManager = MakeUserManager();

        db.TenantInvitations.Add(new TenantInvitation
        {
            TenantId = tenantId,
            Email = "someone.else@example.com",
            NormalizedEmail = "SOMEONE.ELSE@EXAMPLE.COM",
            Role = TenantRole.Member,
            InvitedByUserId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var sut = MakeSut(db, userManager);

        var joined = await sut.AcceptPendingInvitationsAsync(Guid.NewGuid(), "stranger@example.com");

        // Someone else's invitation cannot be claimed by whoever signed up later: the e-mail
        // is what ties the invitation, and it was just proved at sign-up.
        Assert.Equal(0, joined);
        Assert.False(await db.TenantUsers.AnyAsync());
    }

    [Fact]
    public async Task A_second_invitation_to_the_same_pending_address_is_refused()
    {
        var (db, tenantId) = MakeDbWithTenant();
        var userManager = MakeUserManager();
        userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);

        var sut = MakeSut(db, userManager);
        var request = new InviteMemberRequestDto("new@example.com", TenantRole.Member);

        await sut.InviteMemberAsync(tenantId, request, Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.InviteMemberAsync(tenantId, request, Guid.NewGuid()));
    }

    [Fact]
    public async Task Inviting_someone_who_is_already_a_member_is_refused()
    {
        var (db, tenantId) = MakeDbWithTenant();
        var userManager = MakeUserManager();

        var existing = new ApplicationUser { Id = Guid.NewGuid(), Email = "ana@example.com" };
        userManager.FindByEmailAsync("ana@example.com").Returns(existing);

        db.TenantUsers.Add(new TenantUser { TenantId = tenantId, UserId = existing.Id, Role = TenantRole.Member });
        await db.SaveChangesAsync();

        var sut = MakeSut(db, userManager);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InviteMemberAsync(
            tenantId, new InviteMemberRequestDto("ana@example.com", TenantRole.Member), Guid.NewGuid()));
    }
}
