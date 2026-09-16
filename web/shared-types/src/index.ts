// This package holds TypeScript types that both the user app and the admin app need.
// We write them once here instead of copying the same type into both apps.
// Real types get added as we build each feature (see docs/02-api-surface.md for the
// shape of every endpoint response).

// Every money amount coming back from the API is a plain JSON number, for example
// 125000.00. It is never negative, a shortfall shows up as a separate "deficit" field
// instead. Always format this with Intl.NumberFormat and the account's own currency
// and locale, do not just print the raw number.
export type MoneyAmount = number;

// This is the currency, timezone and locale a Natoshare account is set up with.
// It comes back from GET /me and drives how we format every money amount, date and
// number in both apps.
export type AccountLocale = {
  currencyCode: string;
  currencySymbol: string;
  timeZoneId: string;
  locale: string;
};

// One currency option shown in the onboarding currency picker. Any real ISO 4217 code
// is accepted by the API, this list is just a shortlist of common ones for a nicer
// dropdown, it does not limit what a user can type in.
export type CurrencyOption = {
  code: string;
  symbol: string;
  name: string;
};

// A calendar month sent as plain numbers, for example { year: 2026, month: 9 } for
// September 2026. This is how the API talks about "which month a split applies from",
// it is never a Date object.
export type MonthInput = {
  year: number;
  month: number;
};

export type CategoryKind = "Standard" | "FixedAccount";

// One of a user's categories, for example Rent or Feeding. currentPercentage is null
// until the user has an active income split that actually includes this category.
export type Category = {
  id: string;
  name: string;
  kind: CategoryKind;
  externalAccountLabel: string | null;
  subCategories: string[];
  sortOrder: number;
  isArchived: boolean;
  currentPercentage: number | null;
};

export type CategoryAllocationInput = {
  categoryId: string;
  percentage: number;
};

export type AllocationCategoryResult = {
  categoryId: string;
  name: string;
  kind: CategoryKind;
  percentage: number;
  allocatedAmount: MoneyAmount;
};

// One saved income split, either the one active right now or one from the history.
export type AllocationVersionResult = {
  id: string;
  fixedIncomeAmount: MoneyAmount;
  effectiveFromMonth: MonthInput;
  categories: AllocationCategoryResult[];
  affectedOpenMonthReSnapshotted: boolean;
};

export type CurrentAllocationResult = {
  fixedIncomeAmount: MoneyAmount;
  effectiveFromMonth: MonthInput;
  categories: AllocationCategoryResult[];
};

export type AllocationPreviewCategoryResult = {
  categoryId: string;
  percentage: number;
  allocatedAmount: MoneyAmount;
};

export type AllocationPreviewResult = {
  categories: AllocationPreviewCategoryResult[];
  total: MoneyAmount;
};

// One item inside a BudgetTemplate, for example "Rent, FixedAccount, 25%". This has no
// id of its own, it only exists as part of a template.
export type BudgetTemplateItem = {
  name: string;
  kind: CategoryKind;
  percentage: number;
};

// A ready-made split a user can pick during onboarding instead of building their own
// categories from scratch, for example "50/30/20".
export type BudgetTemplate = {
  id: string;
  name: string;
  description: string;
  items: BudgetTemplateItem[];
};

// Tells the onboarding wizard which step to show if a user reloads the page or comes
// back later without finishing.
export type OnboardingStateResult = {
  step: string;
  localeSet: boolean;
  currencySet: boolean;
  incomeSet: boolean;
  categoriesSet: boolean;
  done: boolean;
};

export type CurrencyInput = {
  code: string;
  symbol: string;
};

export type OnboardingCategoryInput = {
  name: string;
  kind: CategoryKind;
  percentage: number;
  externalAccountLabel: string | null;
  subCategories: string[] | null;
};

export type OnboardingCompleteRequest = {
  currency: CurrencyInput;
  timeZoneId: string;
  locale: string | null;
  fixedIncomeAmount: number;
  effectiveFromMonth: MonthInput;
  categories: OnboardingCategoryInput[];
};

// --- Ledger (Phase 3): income, expenses, deficits, balances -----------------------

export type IncomeType = "Allocatable" | "Flexible";
export type ExpenseSource = "Category" | "FlexiblePool";
export type TransactionStatus = "Active" | "Reversed";

export type IncomeSplit = {
  categoryId: string;
  categoryName: string;
  amount: MoneyAmount;
};

export type Income = {
  id: string;
  type: IncomeType;
  amount: MoneyAmount;
  description: string;
  occurredOn: string;
  status: TransactionStatus;
  splits: IncomeSplit[];
};

export type LogIncomeRequest = {
  type: IncomeType;
  amount: number;
  description: string;
  occurredOn: string | null;
};

export type Expense = {
  id: string;
  amount: MoneyAmount;
  description: string;
  source: ExpenseSource;
  categoryId: string | null;
  subCategory: string | null;
  occurredOn: string;
  status: TransactionStatus;
  tags: string[];
};

export type LogExpenseRequest = {
  amount: number;
  description: string;
  source: ExpenseSource;
  categoryId: string | null;
  subCategory: string | null;
  occurredOn: string | null;
  tags: string[] | null;
};

// "OnTrack" | "OverPace" | "InDeficit" | "NotApplicable" (a Flexible Pool expense has
// no category to pace against).
export type PacingStatus = "OnTrack" | "OverPace" | "InDeficit" | "NotApplicable";

export type Pacing = {
  categoryStatus: PacingStatus;
  projected: MoneyAmount;
  safeToSpend: MoneyAmount;
};

export type LogExpenseResult = {
  expense: Expense;
  pacing: Pacing;
  wentIntoDeficit: boolean;
  deficit: { categoryId: string; amount: MoneyAmount } | null;
};

export type SuggestedSource = {
  kind: "OwnSavings" | "OtherCategorySavings" | "FlexiblePool";
  categoryId: string | null;
  availableToUse: MoneyAmount;
};

export type DeficitListItem = {
  categoryId: string;
  name: string;
  amount: MoneyAmount;
  carriedInDeficit: MoneyAmount;
  suggestedSources: SuggestedSource[];
};

// Phase 3 only wires up the three methods that move real money in right now, carrying
// a deficit to next month is a month-close decision (Phase 5).
export type DeficitResolutionMethod = "OwnSavings" | "OtherCategorySavings" | "FlexiblePool";

export type ResolveDeficitRequest = {
  categoryId: string;
  year: number;
  month: number;
  amount: number;
  method: DeficitResolutionMethod;
  sourceCategoryId: string | null;
  note: string | null;
};

export type PaceInfo = {
  projected: MoneyAmount;
  status: PacingStatus;
};

export type SafeToSpend = {
  daily: MoneyAmount;
  weekly: MoneyAmount;
};

export type CategoryBalance = {
  categoryId: string;
  name: string;
  kind: CategoryKind;
  allocated: MoneyAmount;
  carriedInSavings: MoneyAmount;
  carriedInDeficit: MoneyAmount;
  covered: MoneyAmount;
  funded: MoneyAmount;
  spent: MoneyAmount;
  available: MoneyAmount;
  deficit: MoneyAmount;
  savingsBalance: MoneyAmount;
  deployedBalance: MoneyAmount;
  pace: PaceInfo;
  safeToSpend: SafeToSpend;
};

export type BalancesResult = {
  month: { year: number; month: number; status: string };
  categories: CategoryBalance[];
  flexiblePool: { balance: MoneyAmount };
  totals: {
    allocated: MoneyAmount;
    spent: MoneyAmount;
    available: MoneyAmount;
    deficit: MoneyAmount;
    savedToDate: MoneyAmount;
  };
};

// --- Notifications & alerts (Phase 4) -----------------------

// The full vocabulary the backend knows about. Only the first four are ever actually
// produced right now, the rest belong to features that ship in later phases.
export type NotificationKind =
  | "OverPaceCategory"
  | "OverspendCategory"
  | "CategoryInDeficit"
  | "SafeToSpendLow"
  | "MonthCloseReminder"
  | "FixedAccountUnconfirmed"
  | "CarriedDeficitApplied"
  | "MonthEndSummary"
  | "DebtDueSoon"
  | "DebtOverdue"
  | "LoanReturnDueSoon"
  | "LoanOverdue"
  | "PromiseReminder"
  | "RecurringItemDue";

export type NotificationSeverity = "Info" | "Warning" | "Critical";

export type Notification = {
  id: string;
  kind: NotificationKind;
  title: string;
  body: string;
  severity: NotificationSeverity;
  relatedEntityType: string | null;
  relatedEntityId: string | null;
  isRead: boolean;
  readAt: string | null;
  createdAt: string;
};

// Only these four kinds can actually be adjusted right now, the rest have no working
// alert behind them yet.
export type AdjustableAlertKind = "OverPaceCategory" | "OverspendCategory" | "CategoryInDeficit" | "SafeToSpendLow";

export type AlertPreference = {
  kind: AdjustableAlertKind;
  enabled: boolean;
  thresholdPercent: number | null;
  leadDays: number | null;
};

export type UpdateAlertPreferenceInput = {
  kind: AdjustableAlertKind;
  enabled: boolean;
  thresholdPercent: number | null;
  leadDays: number | null;
};

// --- Insights (Phase 4) -----------------------

export type TopCategory = {
  categoryId: string;
  name: string;
  spent: MoneyAmount;
};

export type SpendingTrend = {
  direction: "Up" | "Down" | "Flat";
  percentChange: number | null;
};

export type SpendingSummary = {
  plainEnglish: string;
  year: number;
  month: number;
  topCategories: TopCategory[];
  trend: SpendingTrend;
};

export type PacingInsight = {
  categoryId: string;
  name: string;
  projected: MoneyAmount;
  status: PacingStatus;
  safeToSpendDaily: MoneyAmount;
};
