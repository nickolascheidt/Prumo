using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Prumo.Application.DTOs.Auth;
using Prumo.Application.Services;
using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Services;

namespace Prumo.Tests.Services;

/// <summary>
/// The account lifecycle: sign up without a session, no sign-in before confirming, and
/// not telling strangers which e-mails exist.
///
/// What **cannot** be covered here: the e-mail actually going out. There is no e-mail
/// provider; notifications are written to the log.
/// </summary>
public class AuthServiceSignupTests
{
    private static UserManager<ApplicationUser> MakeUserManager()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        return Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
    }

    private static SignInManager<ApplicationUser> MakeSignInManager(UserManager<ApplicationUser> userManager) =>
        Substitute.For<SignInManager<ApplicationUser>>(
            userManager,
            Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null, null, null, null);

    private static AuthService MakeSut(
        UserManager<ApplicationUser> userManager,
        INotificationPublisher notifications,
        SignInManager<ApplicationUser>? signInManager = null) =>
        new(userManager,
            signInManager ?? MakeSignInManager(userManager),
            Substitute.For<IConfiguration>(),
            Substitute.For<ITenantService>(),
            Substitute.For<ITenantRoleService>(),
            notifications);

    private static ApplicationUser AUser(bool emailConfirmed) => new()
    {
        Id = Guid.NewGuid(),
        Email = "person@example.com",
        UserName = "person@example.com",
        FullName = "Example Person",
        IsActive = true,
        EmailConfirmed = emailConfirmed
    };

    [Fact]
    public async Task Register_publishes_a_confirmation_and_returns_no_session()
    {
        var userManager = MakeUserManager();
        userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        userManager.GenerateEmailConfirmationTokenAsync(Arg.Any<ApplicationUser>())
            .Returns("token-do-identity");

        var notifications = Substitute.For<INotificationPublisher>();
        var sut = MakeSut(userManager, notifications);

        var result = await sut.RegisterAsync(
            new RegisterRequestDto("person@example.com", "Password@123", "Example Person", null), null);

        Assert.Equal("person@example.com", result.Email);

        // The return type has no token — if someone ever returns LoginResponseDto from
        // here, e-mail confirmation becomes decoration and this test stops compiling.
        await notifications.Received(1).PublishAsync(
            Arg.Is<NotificationMessage>(m =>
                m.Type == NotificationTypes.EmailConfirmation
                && m.To == "person@example.com"
                && m.Data.ContainsKey("link")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_with_the_right_password_but_unconfirmed_email_is_refused_distinguishably()
    {
        var user = AUser(emailConfirmed: false);
        var userManager = MakeUserManager();
        userManager.FindByEmailAsync(user.Email!).Returns(user);

        var signInManager = MakeSignInManager(userManager);
        signInManager.CheckPasswordSignInAsync(user, "Password@123", true)
            .Returns(SignInResult.Success);

        var sut = MakeSut(userManager, Substitute.For<INotificationPublisher>(), signInManager);

        // Its own type, not UnauthorizedAccessException: the SPA needs to send this person
        // to "resend confirmation", and the generic 401 would drop the message.
        await Assert.ThrowsAsync<EmailNotConfirmedException>(
            () => sut.LoginAsync(new LoginRequestDto(user.Email!, "Password@123")));
    }

    [Fact]
    public async Task Forgot_password_for_an_unknown_email_publishes_nothing_and_does_not_throw()
    {
        var userManager = MakeUserManager();
        userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);

        var notifications = Substitute.For<INotificationPublisher>();
        var sut = MakeSut(userManager, notifications);

        await sut.ForgotPasswordAsync("nobody@example.com");

        // The endpoint answers 202 either way; what must not happen is an e-mail going out.
        // If someone ever "improves" this by throwing for an unknown e-mail, the endpoint
        // becomes an account enumerator.
        await notifications.DidNotReceive().PublishAsync(
            Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resending_a_confirmation_to_an_already_confirmed_account_publishes_nothing()
    {
        var user = AUser(emailConfirmed: true);
        var userManager = MakeUserManager();
        userManager.FindByEmailAsync(user.Email!).Returns(user);

        var notifications = Substitute.For<INotificationPublisher>();
        var sut = MakeSut(userManager, notifications);

        await sut.ResendConfirmationAsync(user.Email!);

        await notifications.DidNotReceive().PublishAsync(
            Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resetting_the_password_also_confirms_the_email()
    {
        var user = AUser(emailConfirmed: false);
        var userManager = MakeUserManager();
        userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        userManager.ResetPasswordAsync(user, Arg.Any<string>(), "NovaPassword@123")
            .Returns(IdentityResult.Success);

        var sut = MakeSut(userManager, Substitute.For<INotificationPublisher>());

        var encodedToken = Convert.ToBase64String("token-do-identity"u8.ToArray())
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var ok = await sut.ResetPasswordAsync(
            new ResetPasswordRequestDto(user.Id, encodedToken, "NovaPassword@123"));

        Assert.True(ok);

        // Whoever opened the link proved access to the mailbox — which is exactly what
        // confirmation checks. Without this, someone who forgot their password before
        // confirming would reset it and still not be able to sign in.
        Assert.True(user.EmailConfirmed);
    }
}
