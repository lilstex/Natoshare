using Natoshare.Application.Ledger;

namespace Natoshare.Application.PeopleAndMoney;

// --- Loans out ---

public record LoanRepaymentDto(
    Guid Id, decimal Amount, DateOnly ReceivedOn, string? Note, AccountRefInput? LinkedDestination, DateTimeOffset CreatedAt);

public record LoanOutDto(
    Guid Id, string BorrowerName, decimal Amount, DateOnly LentOn, DateOnly? ExpectedReturnOn, string? Note,
    string Status, AccountRefInput? LinkedSource, decimal TotalRepaid, decimal Outstanding, DateTimeOffset CreatedAt,
    List<LoanRepaymentDto> Repayments);

public record CreateLoanOutRequest(
    string BorrowerName, decimal Amount, DateOnly LentOn, DateOnly? ExpectedReturnOn, string? Note, AccountRefInput? LinkedSource);

public record CreateLoanRepaymentRequest(decimal Amount, DateOnly ReceivedOn, string? Note, AccountRefInput? LinkedDestination);

public record UpdateLoanOutRequest(string? BorrowerName, DateOnly? ExpectedReturnOn, string? Note, string? Status);

// --- Debts (borrowed) ---

public record DebtRepaymentDto(
    Guid Id, decimal Amount, DateOnly PaidOn, string? Note, AccountRefInput? LinkedSource, DateTimeOffset CreatedAt);

public record DebtInDto(
    Guid Id, string LenderName, decimal Amount, DateOnly BorrowedOn, DateOnly? DueOn, string? Note,
    string Status, decimal TotalRepaid, decimal Outstanding, DateTimeOffset CreatedAt, List<DebtRepaymentDto> Repayments);

public record CreateDebtInRequest(string LenderName, decimal Amount, DateOnly BorrowedOn, DateOnly? DueOn, string? Note);

public record CreateDebtRepaymentRequest(decimal Amount, DateOnly PaidOn, string? Note, AccountRefInput? LinkedSource);

public record UpdateDebtInRequest(string? LenderName, DateOnly? DueOn, string? Note, string? Status);

// --- Promises ---

public record PromiseRedemptionDto(
    Guid Id, decimal Amount, DateOnly RedeemedOn, AccountRefInput SourceAccount, string? Note, DateTimeOffset CreatedAt);

public record PromiseDto(
    Guid Id, string PersonName, decimal Amount, string? Note, DateOnly MadeOn, string Status,
    decimal TotalRedeemed, decimal Outstanding, DateTimeOffset CreatedAt, List<PromiseRedemptionDto> Redemptions);

public record CreatePromiseRequest(string PersonName, decimal Amount, string? Note, DateOnly MadeOn);

public record CreatePromiseRedemptionRequest(decimal Amount, DateOnly RedeemedOn, AccountRefInput SourceAccount, string? Note);

public record UpdatePromiseRequest(string? PersonName, decimal? Amount, string? Note, string? Status);

// --- Investment log ---

public record InvestmentLogDto(Guid Id, decimal Amount, DateOnly InvestedOn, string Platform, string? Note, DateTimeOffset CreatedAt);

public record CreateInvestmentLogRequest(decimal Amount, DateOnly InvestedOn, string Platform, string? Note);

public record InvestmentByPlatformDto(string Platform, decimal Amount);

public record InvestmentSummaryDto(decimal Allocated, decimal Invested, decimal Shortfall, List<InvestmentByPlatformDto> ByPlatform);

// --- Net position & obligations ---

public record NetPositionBreakdownDto(
    decimal Savings, decimal Deployed, decimal Pool, decimal LoansOut, decimal DebtsIn, decimal OpenPromises, decimal CarriedDeficits);

public record NetPositionDto(decimal Total, NetPositionBreakdownDto Breakdown);

public record ObligationItemDto(DateOnly Date, string Type, string Title, decimal? Amount, Guid? EntityId, string Severity);
