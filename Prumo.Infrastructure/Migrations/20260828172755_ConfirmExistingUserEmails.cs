using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prumo.Infrastructure.Migrations
{
    /// <summary>
    /// Marca como confirmado o e-mail de todo mundo que já existia.
    ///
    /// A partir do item 8 o login exige `EmailConfirmed`. Quem foi criado antes disso — o
    /// admin semeado e todos os membros que o admin cadastrou com senha — nunca passou por
    /// confirmação nenhuma, e sem este backfill ficaria **trancado para fora** de um dia
    /// para o outro, sem ter feito nada.
    ///
    /// É uma migration e não rotina de startup de propósito: precisa rodar **uma vez**.
    /// Repetida a cada boot, ela reconfirmaria contas que alguém tivesse desconfirmado —
    /// a mesma forma dos bugs 4203a15 e do backfill de dashboard.
    /// </summary>
    public partial class ConfirmExistingUserEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "AspNetUsers" SET "EmailConfirmed" = true WHERE "EmailConfirmed" = false;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem reversão: não há como saber quais contas estavam não confirmadas antes,
            // e desconfirmar todas trancaria todo mundo para fora.
        }
    }
}
