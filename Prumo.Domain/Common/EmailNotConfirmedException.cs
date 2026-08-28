namespace Prumo.Domain.Common
{
    /// <summary>
    /// A senha está certa, mas o e-mail nunca foi confirmado.
    ///
    /// É um tipo próprio, e não `UnauthorizedAccessException`, porque o SPA precisa
    /// **distinguir** este caso de "credenciais inválidas" — um manda para a tela de
    /// reenviar confirmação, o outro não pode dizer nada.
    ///
    /// Só é lançada **depois** da checagem de senha. Antes dela, responder "e-mail não
    /// confirmado" contaria a qualquer um que aquele endereço tem conta.
    /// </summary>
    public class EmailNotConfirmedException : Exception
    {
        public const string Code = "email_not_confirmed";

        public EmailNotConfirmedException()
            : base("Confirme seu e-mail para entrar. Reenviamos o link se precisar.")
        {
        }
    }
}
