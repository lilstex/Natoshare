using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Natoshare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerAndTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BudgetMonths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AllocationConfigVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FixedIncomeSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetMonths", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeficitResolutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetMonthId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReallocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeficitResolutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubCategory = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    BudgetMonthId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: false),
                    ResponseBody = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Incomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    BudgetMonthId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incomes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetMonthId = table.Column<Guid>(type: "uuid", nullable: true),
                    Account = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AccountCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntryType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SourceTxnType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceTxnId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReversesLedgerEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reallocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetMonthId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromAccountKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FromAccountCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToAccountKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ToAccountCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reallocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoryMonths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetMonthId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CarriedInSavings = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CarriedInDeficit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SpentAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CoveredAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ExternalTransferConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalTransferAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ExternalTransferConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SavedThisMonth = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DeficitAtClose = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DeficitResolvedVia = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CarriedOutSavings = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CarriedOutDeficit = table.Column<decimal>(type: "numeric(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryMonths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryMonths_BudgetMonths_BudgetMonthId",
                        column: x => x.BudgetMonthId,
                        principalTable: "BudgetMonths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseTags",
                columns: table => new
                {
                    ExpenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseTags", x => new { x.ExpenseId, x.TagId });
                    table.ForeignKey(
                        name: "FK_ExpenseTags_Expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "Expenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncomeSplits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeSplits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomeSplits_Incomes_IncomeId",
                        column: x => x.IncomeId,
                        principalTable: "Incomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetMonths_UserId_Year_Month",
                table: "BudgetMonths",
                columns: new[] { "UserId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoryMonths_BudgetMonthId_CategoryId",
                table: "CategoryMonths",
                columns: new[] { "BudgetMonthId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoryMonths_CategoryId",
                table: "CategoryMonths",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DeficitResolutions_BudgetMonthId_CategoryId",
                table: "DeficitResolutions",
                columns: new[] { "BudgetMonthId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BudgetMonthId",
                table: "Expenses",
                column: "BudgetMonthId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CategoryId",
                table: "Expenses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_UserId_OccurredOn",
                table: "Expenses",
                columns: new[] { "UserId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseTags_TagId",
                table: "ExpenseTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_UserId_Key",
                table: "IdempotencyRecords",
                columns: new[] { "UserId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_BudgetMonthId",
                table: "Incomes",
                column: "BudgetMonthId");

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_UserId_OccurredOn",
                table: "Incomes",
                columns: new[] { "UserId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_IncomeSplits_CategoryId",
                table: "IncomeSplits",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeSplits_IncomeId",
                table: "IncomeSplits",
                column: "IncomeId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_BudgetMonthId",
                table: "LedgerEntries",
                column: "BudgetMonthId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_SourceTxnType_SourceTxnId",
                table: "LedgerEntries",
                columns: new[] { "SourceTxnType", "SourceTxnId" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_UserId_Account_AccountCategoryId",
                table: "LedgerEntries",
                columns: new[] { "UserId", "Account", "AccountCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reallocations_BudgetMonthId",
                table: "Reallocations",
                column: "BudgetMonthId");

            migrationBuilder.CreateIndex(
                name: "IX_Reallocations_UserId_OccurredOn",
                table: "Reallocations",
                columns: new[] { "UserId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Tags_UserId_Name",
                table: "Tags",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryMonths");

            migrationBuilder.DropTable(
                name: "DeficitResolutions");

            migrationBuilder.DropTable(
                name: "ExpenseTags");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "IncomeSplits");

            migrationBuilder.DropTable(
                name: "LedgerEntries");

            migrationBuilder.DropTable(
                name: "Reallocations");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "BudgetMonths");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "Incomes");
        }
    }
}
