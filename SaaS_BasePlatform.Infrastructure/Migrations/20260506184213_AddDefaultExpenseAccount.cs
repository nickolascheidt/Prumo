using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaS_BasePlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultExpenseAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultExpenseAccountId",
                table: "TenantGlSettings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantGlSettings_DefaultExpenseAccountId",
                table: "TenantGlSettings",
                column: "DefaultExpenseAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_TenantGlSettings_Accounts_DefaultExpenseAccountId",
                table: "TenantGlSettings",
                column: "DefaultExpenseAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TenantGlSettings_Accounts_DefaultExpenseAccountId",
                table: "TenantGlSettings");

            migrationBuilder.DropIndex(
                name: "IX_TenantGlSettings_DefaultExpenseAccountId",
                table: "TenantGlSettings");

            migrationBuilder.DropColumn(
                name: "DefaultExpenseAccountId",
                table: "TenantGlSettings");
        }
    }
}
