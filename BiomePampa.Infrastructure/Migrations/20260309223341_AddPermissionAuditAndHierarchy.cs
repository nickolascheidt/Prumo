using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BiomePampa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionAuditAndHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GrantedByUserEmail",
                table: "RolePermissions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GrantedByUserId",
                table: "RolePermissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PermissionAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformedByUserEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionAuditLogs_PerformedAt",
                table: "PermissionAuditLogs",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionAuditLogs_PerformedByUserId",
                table: "PermissionAuditLogs",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionAuditLogs_PermissionId",
                table: "PermissionAuditLogs",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionAuditLogs_RoleId",
                table: "PermissionAuditLogs",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermissionAuditLogs");

            migrationBuilder.DropColumn(
                name: "GrantedByUserEmail",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "GrantedByUserId",
                table: "RolePermissions");
        }
    }
}
