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
