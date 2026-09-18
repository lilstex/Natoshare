using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Natoshare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringItemsAndAccountLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PendingDeletionRequestedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecurringItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    IncomeType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    Cadence = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AnchorDay = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NextRunOn = table.Column<DateOnly>(type: "date", nullable: false),
                    LastPostedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringItems_UserId_IsActive_NextRunOn",
                table: "RecurringItems",
                columns: new[] { "UserId", "IsActive", "NextRunOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecurringItems");

            migrationBuilder.DropColumn(
                name: "PendingDeletionRequestedAt",
                table: "AspNetUsers");
        }
    }
}
