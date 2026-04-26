using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaS_BasePlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdaptExistingFeaturesToTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_RolePermissions",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_Resources_Code",
                table: "Resources");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ResourcePermissions",
                table: "ResourcePermissions");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "RolePermissions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Resources",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ResourcePermissions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PermissionAuditLogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_RolePermissions",
                table: "RolePermissions",
                columns: new[] { "TenantId", "RoleId", "PermissionId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ResourcePermissions",
                table: "ResourcePermissions",
                columns: new[] { "TenantId", "RoleId", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_TenantId",
                table: "RolePermissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_TenantId_Code",
                table: "Resources",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissionAuditLogs_TenantId",
                table: "PermissionAuditLogs",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_RolePermissions",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_TenantId",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_Resources_TenantId_Code",
                table: "Resources");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ResourcePermissions",
                table: "ResourcePermissions");

            migrationBuilder.DropIndex(
                name: "IX_PermissionAuditLogs_TenantId",
                table: "PermissionAuditLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ResourcePermissions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PermissionAuditLogs");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RolePermissions",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ResourcePermissions",
                table: "ResourcePermissions",
                columns: new[] { "RoleId", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Resources_Code",
                table: "Resources",
                column: "Code",
                unique: true);
        }
    }
}
