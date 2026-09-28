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
/// O ciclo de conta do item 8: cadastrar sem sessão, não entrar antes de confirmar, e não
/// contar a estranhos quais e-mails existem.
///
/// O que **não** dá para cobrir aqui: o e-mail sair de verdade. Isso é do serviço de
/// notificação, do outro lado da fila, e está no registro de verificação manual.
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
        Email = "pessoa@exemplo.com",
        UserName = "pessoa@exemplo.com",
        FullName = "Pessoa Exemplo",
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
            new RegisterRequestDto("pessoa@exemplo.com", "Senha@123", "Pessoa Exemplo", null), null);

        Assert.Equal("pessoa@exemplo.com", result.Email);

        // O tipo de retorno não tem token — se um dia alguém devolver LoginResponseDto
        // daqui, a confirmação de e-mail vira decoração e este teste deixa de compilar.
        await notifications.Received(1).PublishAsync(
            Arg.Is<NotificationMessage>(m =>
                m.Type == NotificationTypes.EmailConfirmation
                && m.To == "pessoa@exemplo.com"
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
        signInManager.CheckPasswordSignInAsync(user, "Senha@123", true)
            .Returns(SignInResult.Success);

        var sut = MakeSut(userManager, Substitute.For<INotificationPublisher>(), signInManager);

        // Um tipo próprio, e não UnauthorizedAccessException: o SPA precisa mandar esta
        // pessoa para "reenviar confirmação", e o 401 genérico apaga a mensagem.
        await Assert.ThrowsAsync<EmailNotConfirmedException>(
            () => sut.LoginAsync(new LoginRequestDto(user.Email!, "Senha@123")));
    }

    [Fact]
    public async Task Forgot_password_for_an_unknown_email_publishes_nothing_and_does_not_throw()
    {
        var userManager = MakeUserManager();
        userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);

        var notifications = Substitute.For<INotificationPublisher>();
        var sut = MakeSut(userManager, notifications);

        await sut.ForgotPasswordAsync("nao-existe@exemplo.com");

        // O endpoint responde 202 de qualquer jeito; o que não pode é sair e-mail. Se um
        // dia alguém "melhorar" isso lançando para e-mail desconhecido, o endpoint vira
        // enumerador de contas.
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
        userManager.ResetPasswordAsync(user, Arg.Any<string>(), "NovaSenha@123")
            .Returns(IdentityResult.Success);

        var sut = MakeSut(userManager, Substitute.For<INotificationPublisher>());

        var encodedToken = Convert.ToBase64String("token-do-identity"u8.ToArray())
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var ok = await sut.ResetPasswordAsync(
            new ResetPasswordRequestDto(user.Id, encodedToken, "NovaSenha@123"));

        Assert.True(ok);

        // Quem abriu o link provou ter acesso à caixa de e-mail — que é exatamente o que a
        // confirmação verifica. Sem isto, quem esqueceu a senha antes de confirmar
        // redefiniria e ainda assim não conseguiria entrar.
        Assert.True(user.EmailConfirmed);
    }
}
