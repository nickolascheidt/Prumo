using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BiomePampa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuthFinish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetUserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("a1111111-1111-1111-1111-111111111111"), new Guid("a3333333-3333-3333-3333-333333333333") });

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: new Guid("a3333333-3333-3333-3333-333333333333"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetUsers",
                columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "CreatedAt", "Email", "EmailConfirmed", "FullName", "IsActive", "LastLoginAt", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "SecurityStamp", "TwoFactorEnabled", "UserName" },
                values: new object[] { new Guid("a3333333-3333-3333-3333-333333333333"), 0, "a3333333-3333-3333-3333-333333333333", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@biomepampa.com", true, "Administrador do Sistema", true, null, true, null, "ADMIN@BIOMEPAMPA.COM", "ADMIN@BIOMEPAMPA.COM", "AQAAAAIAAYagAAAAEJ3yzvSKF7fH8PZxNqE0PvBfKDMq0p3L1F5VzJXJ0bH8F3kxqPJZGQxJ9pZqVJKvxQ==", null, false, "a4444444-4444-4444-4444-444444444444", false, "admin@biomepampa.com" });

            migrationBuilder.InsertData(
                table: "AspNetUserRoles",
                columns: new[] { "RoleId", "UserId" },
                values: new object[] { new Guid("a1111111-1111-1111-1111-111111111111"), new Guid("a3333333-3333-3333-3333-333333333333") });
        }
    }
}
