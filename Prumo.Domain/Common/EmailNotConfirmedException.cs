namespace Prumo.Domain.Common
{
    /// <summary>
    /// The password is right, but the e-mail was never confirmed.
    ///
    /// It is its own type, not `UnauthorizedAccessException`, because the SPA needs to
    /// **tell** this case apart from "invalid credentials" — one sends the user to the
    /// resend-confirmation screen, the other must not say anything.
    ///
    /// Only thrown **after** the password check. Before it, answering "e-mail not
    /// confirmed" would tell anyone that the address has an account.
    /// </summary>
    public class EmailNotConfirmedException : Exception
    {
        public const string Code = "email_not_confirmed";

        public EmailNotConfirmedException()
            : base("Confirm your e-mail to sign in. We can resend the link if you need it.")
        {
        }
    }
}
