using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Natoshare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionsAndPlanConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeatureFlags",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlags", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "PlanConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Plan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MaxCategories = table.Column<int>(type: "integer", nullable: true),
                    HistoryWindowDays = table.Column<int>(type: "integer", nullable: true),
                    SinkingFundEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DeficitCoverFromSavingsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RecurringItemsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ExportEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Plan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BillingCycle = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Reference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActivatedByAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanConfigs_Plan",
                table: "PlanConfigs",
                column: "Plan",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRecords_Reference",
                table: "SubscriptionRecords",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRecords_UserId_Status",
                table: "SubscriptionRecords",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureFlags");

            migrationBuilder.DropTable(
                name: "PlanConfigs");

            migrationBuilder.DropTable(
                name: "SubscriptionRecords");
        }
    }
}
