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
  // Over the Free plan's category limit, data is kept, only new spending against
  // it is refused, see IEntitlementService on the backend.
  isLocked: boolean;
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
  isLocked: boolean;
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

// As of Phase 7 every kind in the NotificationKind vocabulary is adjustable, none
// are left waiting on a later phase.
export type AdjustableAlertKind =
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

// --- Month lifecycle (Phase 5) -----------------------

export type MonthStatus = "Open" | "Closed";

export type MonthSummaryListItem = {
  year: number;
  month: number;
  status: MonthStatus;
  fixedIncomeSnapshot: MoneyAmount;
};

export type CategoryMonthDetail = {
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
  externalTransferConfirmed: boolean;
  externalTransferAmount: MoneyAmount | null;
  externalTransferConfirmedAt: string | null;
  savedThisMonth: MoneyAmount | null;
  deficitAtClose: MoneyAmount | null;
  deficitResolvedVia: string | null;
  carriedOutSavings: MoneyAmount | null;
  carriedOutDeficit: MoneyAmount | null;
};

export type MonthDetail = {
  year: number;
  month: number;
  status: MonthStatus;
  fixedIncomeSnapshot: MoneyAmount;
  closedAt: string | null;
  categories: CategoryMonthDetail[];
};

export type FixedAccountToConfirm = {
  categoryId: string;
  name: string;
  allocated: MoneyAmount;
};

export type ProjectedSaving = {
  categoryId: string;
  name: string;
  saved: MoneyAmount;
};

export type ClosePreview = {
  missingIncomeHint: string | null;
  fixedAccountsToConfirm: FixedAccountToConfirm[];
  projectedSavings: ProjectedSaving[];
  deficits: DeficitListItem[];
  totalSavings: MoneyAmount;
};

export type ConfirmFixedAccountRequest = {
  categoryId: string;
  amount: number;
  transferredOn: string;
};

export type CloseDeficitResolutionInput = {
  categoryId: string;
  amount: number;
  method: DeficitResolutionMethod;
  sourceCategoryId: string | null;
  note: string | null;
};

export type CloseMonthRequest = {
  fixedAccountConfirmations: ConfirmFixedAccountRequest[] | null;
  deficitResolutions: CloseDeficitResolutionInput[];
  promiseRedemptions: null;
  rebalances: null;
};

export type CloseMonthResult = {
  year: number;
  month: number;
  status: MonthStatus;
  closedAt: string;
  categories: CategoryMonthDetail[];
};

// --- Phase 6: people & money -------------------------------------------------

export type AccountRefKind = "Category" | "CategorySavings" | "FlexiblePool";

export type AccountRefInput = {
  kind: AccountRefKind;
  categoryId: string | null;
};

export type LoanOutStatus = "Outstanding" | "PartiallyRepaid" | "Repaid" | "WrittenOff";

export type LoanRepayment = {
  id: string;
  amount: MoneyAmount;
  receivedOn: string;
  note: string | null;
  linkedDestination: AccountRefInput | null;
  createdAt: string;
};

export type LoanOut = {
  id: string;
  borrowerName: string;
  amount: MoneyAmount;
  lentOn: string;
  expectedReturnOn: string | null;
  note: string | null;
  status: LoanOutStatus;
  linkedSource: AccountRefInput | null;
  totalRepaid: MoneyAmount;
  outstanding: MoneyAmount;
  createdAt: string;
  repayments: LoanRepayment[];
};

export type CreateLoanOutRequest = {
  borrowerName: string;
  amount: number;
  lentOn: string;
  expectedReturnOn: string | null;
  note: string | null;
  linkedSource: AccountRefInput | null;
};

export type CreateLoanRepaymentRequest = {
  amount: number;
  receivedOn: string;
  note: string | null;
  linkedDestination: AccountRefInput | null;
};

export type DebtInStatus = "Outstanding" | "PartiallyRepaid" | "Repaid";

export type DebtRepayment = {
  id: string;
  amount: MoneyAmount;
  paidOn: string;
  note: string | null;
  linkedSource: AccountRefInput | null;
  createdAt: string;
};

export type DebtIn = {
  id: string;
  lenderName: string;
  amount: MoneyAmount;
  borrowedOn: string;
  dueOn: string | null;
  note: string | null;
  status: DebtInStatus;
  totalRepaid: MoneyAmount;
  outstanding: MoneyAmount;
  createdAt: string;
  repayments: DebtRepayment[];
};

export type CreateDebtInRequest = {
  lenderName: string;
  amount: number;
  borrowedOn: string;
  dueOn: string | null;
  note: string | null;
};

export type CreateDebtRepaymentRequest = {
  amount: number;
  paidOn: string;
  note: string | null;
  linkedSource: AccountRefInput | null;
};

export type PromiseStatus = "Open" | "PartiallyRedeemed" | "Redeemed" | "Cancelled";

export type PromiseRedemption = {
  id: string;
  amount: MoneyAmount;
  redeemedOn: string;
  sourceAccount: AccountRefInput;
  note: string | null;
  createdAt: string;
};

// Named MoneyPromise, not Promise, so it never shadows the built-in JavaScript
// Promise<T> in any file that imports it.
export type MoneyPromise = {
  id: string;
  personName: string;
  amount: MoneyAmount;
  note: string | null;
  madeOn: string;
  status: PromiseStatus;
  totalRedeemed: MoneyAmount;
  outstanding: MoneyAmount;
  createdAt: string;
  redemptions: PromiseRedemption[];
};

export type CreatePromiseRequest = {
  personName: string;
  amount: number;
  note: string | null;
  madeOn: string;
};

export type CreatePromiseRedemptionRequest = {
  amount: number;
  redeemedOn: string;
  sourceAccount: AccountRefInput;
  note: string | null;
};

export type InvestmentLog = {
  id: string;
  amount: MoneyAmount;
  investedOn: string;
  platform: string;
  note: string | null;
  createdAt: string;
};

export type CreateInvestmentLogRequest = {
  amount: number;
  investedOn: string;
  platform: string;
  note: string | null;
};

export type InvestmentSummary = {
  allocated: MoneyAmount;
  invested: MoneyAmount;
  shortfall: MoneyAmount;
  byPlatform: { platform: string; amount: MoneyAmount }[];
};

export type NetPosition = {
  total: MoneyAmount;
  breakdown: {
    savings: MoneyAmount;
    deployed: MoneyAmount;
    pool: MoneyAmount;
    loansOut: MoneyAmount;
    debtsIn: MoneyAmount;
    openPromises: MoneyAmount;
    carriedDeficits: MoneyAmount;
  };
};

export type ObligationType =
  | "DebtDue"
  | "LoanReturn"
  | "PromiseReminder"
  | "RecurringItem"
  | "FixedAccountConfirm"
  | "MonthClose"
  | "CarriedDeficit";

export type ObligationItem = {
  date: string;
  type: ObligationType;
  title: string;
  amount: MoneyAmount | null;
  entityId: string | null;
  severity: "Info" | "Warning" | "Critical";
};

// --- Phase 7: recurring items -------------------------------------------------

export type RecurringItemKind = "Expense" | "Income";

export type RecurringCadence = "Monthly" | "Weekly" | "BiWeekly";

export type RecurringItemMode = "Remind" | "AutoPost";

export type RecurringItem = {
  id: string;
  kind: RecurringItemKind;
  amount: MoneyAmount;
  description: string;
  categoryId: string | null;
  incomeType: IncomeType | null;
  cadence: RecurringCadence;
  // For Monthly, 0 means "end of month", 1-28 means that day of the month. For
  // Weekly and BiWeekly, this is a day of week, Sunday = 0 through Saturday = 6.
  anchorDay: number;
  mode: RecurringItemMode;
  nextRunOn: string;
  lastPostedOn: string | null;
  isActive: boolean;
  createdAt: string;
};

export type CreateRecurringItemRequest = {
  kind: RecurringItemKind;
  amount: number;
  description: string;
  categoryId: string | null;
  incomeType: IncomeType | null;
  cadence: RecurringCadence;
  anchorDay: number;
  mode: RecurringItemMode;
};

export type UpdateRecurringItemRequest = {
  amount?: number | null;
  description?: string | null;
  categoryId?: string | null;
  incomeType?: IncomeType | null;
  cadence?: RecurringCadence | null;
  anchorDay?: number | null;
  mode?: RecurringItemMode | null;
  isActive?: boolean | null;
};

export type CommittedTotalItem = {
  id: string;
  description: string;
  monthlyAmount: MoneyAmount;
};

export type CommittedTotal = {
  monthlyExpenseTotal: MoneyAmount;
  items: CommittedTotalItem[];
};

// --- Phase 8: dashboard & reports -------------------------------------------------

export type RecentTransaction = {
  id: string;
  description: string;
  amount: MoneyAmount;
  isIncome: boolean;
  occurredOn: string;
};

export type OpenPromiseSummary = {
  id: string;
  personName: string;
  outstanding: MoneyAmount;
};

export type Dashboard = {
  netPosition: NetPosition;
  month: { year: number; month: number; status: string };
  categoryCards: CategoryBalance[];
  pacingOverview: PacingInsight[];
  flexiblePool: { balance: MoneyAmount };
  obligations: ObligationItem[];
  openPromises: OpenPromiseSummary[];
  outstandingLoansOut: MoneyAmount;
  debtsOwed: MoneyAmount;
  recentTransactions: RecentTransaction[];
  unreadAlerts: number;
};

export type ReportCategoryStatus = "Over" | "Under" | "Unused";

export type ReportCategoryLine = {
  categoryId: string;
  categoryName: string;
  budget: MoneyAmount;
  actual: MoneyAmount;
  saved: MoneyAmount;
  deficit: MoneyAmount;
  variance: number;
  status: ReportCategoryStatus;
  savingsRate: number;
  prevMonthDelta: number | null;
};

export type ReportDeficitCoverageLine = {
  categoryId: string;
  categoryName: string;
  amount: MoneyAmount;
  // A read-only value coming back from a report, so this can be any method that
  // has ever cleared a deficit (including NextMonthAllocation, Mixed and None),
  // unlike DeficitResolutionMethod above which is only the subset a request body
  // is allowed to send.
  method: string;
  sourceCategoryId: string | null;
  carriedForward: boolean;
};

export type MonthlyReport = {
  year: number;
  month: number;
  isClosed: boolean;
  categories: ReportCategoryLine[];
  deficitsAndCoverage: ReportDeficitCoverageLine[];
  totalBudget: MoneyAmount;
  totalActual: MoneyAmount;
  totalSaved: MoneyAmount;
  overallSavingsRate: number;
};

export type RangeReport = {
  fromYear: number;
  fromMonth: number;
  toYear: number;
  toMonth: number;
  months: MonthlyReport[];
  totalBudget: MoneyAmount;
  totalActual: MoneyAmount;
  totalSaved: MoneyAmount;
  overallSavingsRate: number;
};

export type AnnualReport = {
  year: number;
  months: MonthlyReport[];
  totalBudget: MoneyAmount;
  totalActual: MoneyAmount;
  totalSaved: MoneyAmount;
  overallSavingsRate: number;
};

// --- Phase 9: subscription tiers & gating -------------------------------------------------

export type PlanName = "Free" | "Pro";

export type PlanEntitlements = {
  plan: PlanName;
  isTrial: boolean;
  trialEndsAt: string;
  maxCategories: number | null;
  historyWindowDays: number | null;
  sinkingFund: boolean;
  deficitCoverFromSavings: boolean;
  recurring: boolean;
  export: boolean;
};

export type SubscriptionStatus = {
  plan: PlanName;
  status: "None" | "Pending" | "Active" | "Cancelled" | "Expired";
  isTrial: boolean;
  trialEndsAt: string;
  periodEnd: string | null;
};

export type SubscriptionRecord = {
  id: string;
  plan: PlanName;
  billingCycle: "Monthly" | "Annual";
  status: "Pending" | "Active" | "Cancelled" | "Expired";
  reference: string;
  requestedAt: string;
  activatedAt: string | null;
  periodEnd: string | null;
};

export type UpgradeSubscriptionRequest = {
  plan: "Pro";
  billingCycle: "monthly" | "annual";
};

export type UpgradeResult = {
  reference: string;
  status: string;
  message: string;
};

// --- Phase 10: admin app ------------------------------------------------------------------

export type UserStatusName = "Active" | "Suspended" | "PendingDeletion";

export type AdminUserListItem = {
  id: string;
  email: string;
  displayName: string;
  status: UserStatusName;
  role: string;
  plan: PlanName;
  isTrial: boolean;
  createdAt: string;
};

export type AdminUserListResult = {
  items: AdminUserListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type AdminAuditEvent = {
  id: string;
  actorUserId: string | null;
  actorRole: string;
  action: string;
  entityType: string;
  entityId: string;
  before: string | null;
  after: string | null;
  ip: string | null;
  createdAt: string;
};

export type AdminUserDetail = {
  id: string;
  email: string;
  displayName: string;
  status: UserStatusName;
  role: string;
  timeZoneId: string;
  currencyCode: string;
  createdAt: string;
  trialEndsAt: string;
  onboardingCompletedAt: string | null;
  entitlements: PlanEntitlements;
  currentMonth: {
    month: { year: number; month: number; status: string };
    categories: Array<{ categoryId: string; name: string; allocated: MoneyAmount; spent: MoneyAmount; available: MoneyAmount; deficit: MoneyAmount }>;
    flexiblePool: { balance: MoneyAmount };
    totals: { allocated: MoneyAmount; spent: MoneyAmount; available: MoneyAmount; deficit: MoneyAmount; savedToDate: MoneyAmount };
  };
  subscriptionHistory: SubscriptionRecord[];
  recentActivity: AdminAuditEvent[];
  deficitResolutionCount: number;
  configVersionCount: number;
  unreadNotificationCount: number;
  alertPreferenceCount: number;
};

export type AdminSubscriptionRecord = SubscriptionRecord & {
  userId: string;
  userEmail: string;
  userDisplayName: string;
};

export type AdminAuditSearchResult = {
  items: AdminAuditEvent[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type AdminPlanConfig = {
  plan: PlanName;
  maxCategories: number | null;
  historyWindowDays: number | null;
  sinkingFund: boolean;
  deficitCoverFromSavings: boolean;
  recurring: boolean;
  export: boolean;
};

export type AdminFeatureFlag = {
  key: string;
  enabled: boolean;
};

export type SystemSetting = {
  key: string;
  value: string;
  updatedAt: string;
};

export type AdminMetrics = {
  totalUsers: number;
  signupsLast7Days: number;
  signupsLast30Days: number;
  activeUsersLast30Days: number;
  trialUsers: number;
  proUsers: number;
  suspendedUsers: number;
  pendingSubscriptions: number;
  monthCloseRateLast30Days: number;
};

export type AdminHealth = {
  databaseStatus: string;
  hangfireStatus: string;
  checkedAt: string;
};

export type AdminFailedJob = {
  jobId: string;
  jobName: string;
  exceptionMessage: string | null;
  failedAt: string | null;
};

export type AdminJobs = {
  enqueuedCount: number;
  processingCount: number;
  succeededCount: number;
  failedCount: number;
  recentFailures: AdminFailedJob[];
};

export type IntegrityCheckStatus = {
  lastRanAt: string | null;
  lastCheckedCount: number | null;
  lastDriftCount: number | null;
};

export type AdminErrorLogEntry = {
  timestamp: string;
  level: string;
  message: string;
  exception: string | null;
};

export type RecomputeDiffItem = {
  categoryId: string;
  categoryName: string;
  storedSpent: number;
  recomputedSpent: number;
  hasDrift: boolean;
};

export type RecomputeBalancesResult = {
  year: number;
  month: number;
  diffs: RecomputeDiffItem[];
};
