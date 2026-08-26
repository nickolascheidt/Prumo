using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prumo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResourcePermissionAndSupportAccessAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResourcePermissionAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PreviousLevel = table.Column<int>(type: "integer", nullable: false),
                    NewLevel = table.Column<int>(type: "integer", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformedByUserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourcePermissionAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupportAccessLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MasterAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MasterAdminEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportAccessLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissionAuditLogs_ResourceId",
                table: "ResourcePermissionAuditLogs",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissionAuditLogs_RoleId",
                table: "ResourcePermissionAuditLogs",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissionAuditLogs_TenantId",
                table: "ResourcePermissionAuditLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissionAuditLogs_TenantId_PerformedAt",
                table: "ResourcePermissionAuditLogs",
                columns: new[] { "TenantId", "PerformedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportAccessLogs_GrantedAt",
                table: "SupportAccessLogs",
                column: "GrantedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SupportAccessLogs_MasterAdminUserId",
                table: "SupportAccessLogs",
                column: "MasterAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportAccessLogs_TenantId",
                table: "SupportAccessLogs",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourcePermissionAuditLogs");

            migrationBuilder.DropTable(
                name: "SupportAccessLogs");
        }
    }
}
