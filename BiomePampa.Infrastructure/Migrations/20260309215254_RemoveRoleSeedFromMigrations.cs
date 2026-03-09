using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BiomePampa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRoleSeedFromMigrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("a1111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("a2222222-2222-2222-2222-222222222222"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "CreatedAt", "Description", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { new Guid("a1111111-1111-1111-1111-111111111111"), "a1111111-1111-1111-1111-111111111111", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Acesso total ao sistema", "Administrador", "ADMINISTRADOR" },
                    { new Guid("a2222222-2222-2222-2222-222222222222"), "a2222222-2222-2222-2222-222222222222", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Acesso limitado ao sistema", "Usuario", "USUARIO" }
                });
        }
    }
}
