"use client";

import { useEffect, useState } from "react";
import type {
  AccountRefInput,
  CategoryBalance,
  CreateDebtInRequest,
  CreateDebtRepaymentRequest,
  CreateLoanOutRequest,
  CreateLoanRepaymentRequest,
  CreatePromiseRedemptionRequest,
  CreatePromiseRequest,
  DebtIn,
  LoanOut,
  MoneyPromise,
} from "@natoshare/shared-types";
import { SiteHeader } from "@/components/site-header";
import { Button } from "@/components/ui/button";
import { SelectField } from "@/components/ui/select-field";
import { TextField } from "@/components/ui/text-field";
import { apiFetch, ApiError } from "@/lib/api-client";
import { formatMoney } from "@/lib/format";
import { useAuthStore } from "@/store/auth-store";

type Tab = "Loans" | "Debts" | "Promises";
type MeResponse = { user: { currencyCode: string; locale: string } };

// The three "people & money" ledgers Natoshare tracks outside the monthly budget:
// money lent out, money borrowed, and promises made but not yet paid. See
// docs/01-domain-model.md section 1.6 for how each one settles.
export function PeopleMoneyScreen() {
  const accessToken = useAuthStore((state) => state.accessToken);
  const [tab, setTab] = useState<Tab>("Loans");
  const [accountLocale, setAccountLocale] = useState({ currencyCode: "USD", locale: "en" });
  const [categories, setCategories] = useState<CategoryBalance[]>([]);

  useEffect(() => {
    if (!accessToken) {
      return;
    }

    apiFetch<MeResponse>("/me", { token: accessToken })
      .then((me) => setAccountLocale({ currencyCode: me.user.currencyCode, locale: me.user.locale }))
      .catch(() => {});

    apiFetch<{ categories: CategoryBalance[] }>("/balances", { token: accessToken })
      .then((result) => setCategories(result.categories))
      .catch(() => {});
  }, [accessToken]);

  return (
    <div>
      <SiteHeader />

      <main className="mx-auto max-w-4xl px-4 pb-16">
        <h1 className="text-2xl font-bold text-text">Loans, debts & promises</h1>

        <div className="mt-4 flex gap-2 border-b border-border">
          {(["Loans", "Debts", "Promises"] as Tab[]).map((t) => (
            <button
              key={t}
              type="button"
              onClick={() => setTab(t)}
              className={`px-4 py-2.5 text-sm font-semibold ${
                tab === t ? "border-b-2 border-primary text-text" : "text-muted hover:text-text"
              }`}
            >
              {t}
            </button>
          ))}
        </div>

        <div className="mt-5">
          {tab === "Loans" && (
            <LoansPanel accessToken={accessToken} categories={categories} currencyCode={accountLocale.currencyCode} locale={accountLocale.locale} />
          )}
          {tab === "Debts" && (
            <DebtsPanel accessToken={accessToken} categories={categories} currencyCode={accountLocale.currencyCode} locale={accountLocale.locale} />
          )}
          {tab === "Promises" && (
            <PromisesPanel accessToken={accessToken} categories={categories} currencyCode={accountLocale.currencyCode} locale={accountLocale.locale} />
          )}
        </div>
      </main>
    </div>
  );
}

const STATUS_STYLES: Record<string, string> = {
  Outstanding: "bg-warning-tint text-warning",
  PartiallyRepaid: "bg-warning-tint text-warning",
  PartiallyRedeemed: "bg-warning-tint text-warning",
  Repaid: "bg-success-tint text-success",
  Redeemed: "bg-success-tint text-success",
  Open: "bg-surface-2 text-subtle",
  WrittenOff: "bg-danger-tint text-danger",
  Cancelled: "bg-danger-tint text-danger",
};

// A "none / FlexiblePool / a category / that category's savings" picker, shared by
// every form here that takes an optional or required AccountRef.
function AccountRefPicker({
  categories,
  value,
  onChange,
  allowNone,
}: {
  categories: CategoryBalance[];
  value: AccountRefInput | null;
  onChange: (value: AccountRefInput | null) => void;
  allowNone: boolean;
}) {
  const encoded = value === null ? "none" : value.categoryId ? `${value.kind}:${value.categoryId}` : value.kind;

  function handleChange(raw: string) {
    if (raw === "none") {
      onChange(null);
      return;
    }
    if (raw === "FlexiblePool") {
      onChange({ kind: "FlexiblePool", categoryId: null });
      return;
    }
    const [kind, categoryId] = raw.split(":");
    onChange({ kind: kind as AccountRefInput["kind"], categoryId });
  }

  return (
    <SelectField label="Account" value={encoded} onChange={(e) => handleChange(e.target.value)}>
      {allowNone && <option value="none">Not linked to an account</option>}
      <option value="FlexiblePool">Flexible Pool</option>
      {categories.map((c) => (
        <option key={`cat-${c.categoryId}`} value={`Category:${c.categoryId}`}>
          {c.name}
        </option>
      ))}
      {categories.map((c) => (
        <option key={`sav-${c.categoryId}`} value={`CategorySavings:${c.categoryId}`}>
          {c.name} savings
        </option>
      ))}
    </SelectField>
  );
}

function accountLabel(account: AccountRefInput | null, categories: CategoryBalance[]): string {
  if (!account) {
    return "not linked";
  }
  if (account.kind === "FlexiblePool") {
    return "Flexible Pool";
  }
  const category = categories.find((c) => c.categoryId === account.categoryId);
  const name = category?.name ?? "a category";
  return account.kind === "CategorySavings" ? `${name} savings` : name;
}

// --- Loans out ----------------------------------------------------------------

function LoansPanel({
  accessToken,
  categories,
  currencyCode,
  locale,
}: {
  accessToken: string | null;
  categories: CategoryBalance[];
  currencyCode: string;
  locale: string;
}) {
  const [loans, setLoans] = useState<LoanOut[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [repayingId, setRepayingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  function load() {
    if (!accessToken) {
      return;
    }
    apiFetch<LoanOut[]>("/loans-out", { token: accessToken })
      .then((result) => {
        setLoans(result);
        setError(null);
      })
      .catch((err) => setError(err instanceof ApiError ? err.message : "Could not load loans."));
  }

  useEffect(load, [accessToken]);

  async function writeOff(loanId: string) {
    try {
      await apiFetch(`/loans-out/${loanId}`, { method: "PATCH", token: accessToken, body: { status: "WrittenOff" } });
      load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not write that off.");
    }
  }

  return (
    <div>
      <div className="flex justify-end">
        <Button type="button" onClick={() => setShowForm((v) => !v)}>
          {showForm ? "Cancel" : "Lend money"}
        </Button>
      </div>

      {error && <p className="mt-3 rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

      {showForm && (
        <NewLoanForm
          accessToken={accessToken}
          categories={categories}
          onDone={() => {
            setShowForm(false);
            load();
          }}
          onCancel={() => setShowForm(false)}
        />
      )}

      <div className="mt-4 flex flex-col gap-3">
        {loans.length === 0 && !showForm && <p className="text-sm text-muted">Nothing lent out yet.</p>}
        {loans.map((loan) => (
          <div key={loan.id} className="rounded-xl border border-border bg-surface p-4">
            <div className="flex items-center justify-between">
              <span className="font-semibold text-text">{loan.borrowerName}</span>
              <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${STATUS_STYLES[loan.status]}`}>{loan.status}</span>
            </div>
            <p className="mt-1 text-sm text-muted">
              {formatMoney(loan.amount, currencyCode, locale)} lent on {loan.lentOn}
              {loan.expectedReturnOn ? `, expected back ${loan.expectedReturnOn}` : ""}
              {loan.linkedSource ? ` · from ${accountLabel(loan.linkedSource, categories)}` : ""}
            </p>
            {loan.outstanding > 0 && (
              <p className="mt-1 text-sm font-medium text-text">{formatMoney(loan.outstanding, currencyCode, locale)} still outstanding</p>
            )}

            {loan.repayments.length > 0 && (
              <ul className="mt-2 flex flex-col gap-1 text-xs text-muted">
                {loan.repayments.map((r) => (
                  <li key={r.id}>
                    {formatMoney(r.amount, currencyCode, locale)} received {r.receivedOn}
                    {r.linkedDestination ? ` into ${accountLabel(r.linkedDestination, categories)}` : ""}
                  </li>
                ))}
              </ul>
            )}

            {loan.status !== "Repaid" && loan.status !== "WrittenOff" && (
              <div className="mt-3 flex gap-2">
                <Button type="button" variant="secondary" className="h-8 text-xs" onClick={() => setRepayingId(repayingId === loan.id ? null : loan.id)}>
                  Record repayment
                </Button>
                <Button type="button" variant="secondary" className="h-8 text-xs" onClick={() => writeOff(loan.id)}>
                  Write off
                </Button>
              </div>
            )}

            {repayingId === loan.id && (
              <RepaymentForm
                categories={categories}
                onCancel={() => setRepayingId(null)}
                onSubmit={async (amount, occurredOn, account) => {
                  const request: CreateLoanRepaymentRequest = { amount, receivedOn: occurredOn, note: null, linkedDestination: account };
                  await apiFetch(`/loans-out/${loan.id}/repayments`, { method: "POST", token: accessToken, body: request });
                  setRepayingId(null);
                  load();
                }}
              />
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

function NewLoanForm({
  accessToken,
  categories,
  onDone,
  onCancel,
}: {
  accessToken: string | null;
  categories: CategoryBalance[];
  onDone: () => void;
  onCancel: () => void;
}) {
  const [borrowerName, setBorrowerName] = useState("");
  const [amount, setAmount] = useState("");
  const [expectedReturnOn, setExpectedReturnOn] = useState("");
  const [linkedSource, setLinkedSource] = useState<AccountRefInput | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit() {
    if (!borrowerName.trim() || !amount || Number(amount) <= 0) {
      setError("Enter who you lent to and a valid amount.");
      return;
    }

    setIsSubmitting(true);
    setError(null);

    const request: CreateLoanOutRequest = {
      borrowerName: borrowerName.trim(),
      amount: Number(amount),
      lentOn: new Date().toISOString().slice(0, 10),
      expectedReturnOn: expectedReturnOn || null,
      note: null,
      linkedSource,
    };

    try {
      await apiFetch("/loans-out", { method: "POST", token: accessToken, body: request });
      onDone();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not record that loan.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="mt-4 rounded-xl border border-border bg-surface p-4">
      <p className="text-sm font-semibold text-text">Lend money</p>
      <div className="mt-2 grid grid-cols-1 gap-2 sm:grid-cols-2">
        <TextField label="Borrower" value={borrowerName} onChange={(e) => setBorrowerName(e.target.value)} />
        <TextField label="Amount" type="number" min="0" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
        <TextField label="Expected return (optional)" type="date" value={expectedReturnOn} onChange={(e) => setExpectedReturnOn(e.target.value)} />
        <AccountRefPicker categories={categories} value={linkedSource} onChange={setLinkedSource} allowNone />
      </div>
      {error && <p className="mt-2 text-sm text-danger">{error}</p>}
      <div className="mt-3 flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="button" onClick={handleSubmit} disabled={isSubmitting}>
          {isSubmitting ? "Saving…" : "Lend money"}
        </Button>
      </div>
    </div>
  );
}

function RepaymentForm({
  categories,
  onCancel,
  onSubmit,
}: {
  categories: CategoryBalance[];
  onCancel: () => void;
  onSubmit: (amount: number, occurredOn: string, account: AccountRefInput | null) => Promise<void>;
}) {
  const [amount, setAmount] = useState("");
  const [account, setAccount] = useState<AccountRefInput | null>({ kind: "FlexiblePool", categoryId: null });
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit() {
    if (!amount || Number(amount) <= 0) {
      setError("Enter a valid amount.");
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      await onSubmit(Number(amount), new Date().toISOString().slice(0, 10), account);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not record that.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="mt-3 rounded-lg bg-surface-2 p-3">
      <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
        <TextField label="Amount" type="number" min="0" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
        <AccountRefPicker categories={categories} value={account} onChange={setAccount} allowNone />
      </div>
      {error && <p className="mt-2 text-xs text-danger">{error}</p>}
      <div className="mt-3 flex justify-end gap-2">
        <Button type="button" variant="secondary" className="h-8 text-xs" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="button" className="h-8 text-xs" onClick={handleSubmit} disabled={isSubmitting}>
          {isSubmitting ? "Saving…" : "Record"}
        </Button>
      </div>
    </div>
  );
}

// --- Debts ----------------------------------------------------------------

function DebtsPanel({
  accessToken,
  categories,
  currencyCode,
  locale,
}: {
  accessToken: string | null;
  categories: CategoryBalance[];
  currencyCode: string;
  locale: string;
}) {
  const [debts, setDebts] = useState<DebtIn[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [repayingId, setRepayingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  function load() {
    if (!accessToken) {
      return;
    }
    apiFetch<DebtIn[]>("/debts", { token: accessToken })
      .then((result) => {
        setDebts(result);
        setError(null);
      })
      .catch((err) => setError(err instanceof ApiError ? err.message : "Could not load debts."));
  }

  useEffect(load, [accessToken]);

  return (
    <div>
      <div className="flex justify-end">
        <Button type="button" onClick={() => setShowForm((v) => !v)}>
          {showForm ? "Cancel" : "Record a debt"}
        </Button>
      </div>

      {error && <p className="mt-3 rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

      {showForm && (
        <NewDebtForm
          accessToken={accessToken}
          onDone={() => {
            setShowForm(false);
            load();
          }}
          onCancel={() => setShowForm(false)}
        />
      )}

      <div className="mt-4 flex flex-col gap-3">
        {debts.length === 0 && !showForm && <p className="text-sm text-muted">Nothing borrowed yet.</p>}
        {debts.map((debt) => (
          <div key={debt.id} className="rounded-xl border border-border bg-surface p-4">
            <div className="flex items-center justify-between">
              <span className="font-semibold text-text">{debt.lenderName}</span>
              <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${STATUS_STYLES[debt.status]}`}>{debt.status}</span>
            </div>
            <p className="mt-1 text-sm text-muted">
              {formatMoney(debt.amount, currencyCode, locale)} borrowed on {debt.borrowedOn}
              {debt.dueOn ? `, due ${debt.dueOn}` : ""}
            </p>
            {debt.outstanding > 0 && (
              <p className="mt-1 text-sm font-medium text-text">{formatMoney(debt.outstanding, currencyCode, locale)} still owed</p>
            )}

            {debt.repayments.length > 0 && (
              <ul className="mt-2 flex flex-col gap-1 text-xs text-muted">
                {debt.repayments.map((r) => (
                  <li key={r.id}>
                    {formatMoney(r.amount, currencyCode, locale)} paid {r.paidOn}
                    {r.linkedSource ? ` from ${accountLabel(r.linkedSource, categories)}` : ""}
                  </li>
                ))}
              </ul>
            )}

            {debt.status !== "Repaid" && (
              <div className="mt-3">
                <Button type="button" variant="secondary" className="h-8 text-xs" onClick={() => setRepayingId(repayingId === debt.id ? null : debt.id)}>
                  Record repayment
                </Button>
              </div>
            )}

            {repayingId === debt.id && (
              <RepaymentForm
                categories={categories}
                onCancel={() => setRepayingId(null)}
                onSubmit={async (amount, occurredOn, account) => {
                  const request: CreateDebtRepaymentRequest = { amount, paidOn: occurredOn, note: null, linkedSource: account };
                  await apiFetch(`/debts/${debt.id}/repayments`, { method: "POST", token: accessToken, body: request });
                  setRepayingId(null);
                  load();
                }}
              />
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

function NewDebtForm({ accessToken, onDone, onCancel }: { accessToken: string | null; onDone: () => void; onCancel: () => void }) {
  const [lenderName, setLenderName] = useState("");
  const [amount, setAmount] = useState("");
  const [dueOn, setDueOn] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit() {
    if (!lenderName.trim() || !amount || Number(amount) <= 0) {
      setError("Enter who you borrowed from and a valid amount.");
      return;
    }

    setIsSubmitting(true);
    setError(null);

    const request: CreateDebtInRequest = {
      lenderName: lenderName.trim(),
      amount: Number(amount),
      borrowedOn: new Date().toISOString().slice(0, 10),
      dueOn: dueOn || null,
      note: null,
    };

    try {
      await apiFetch("/debts", { method: "POST", token: accessToken, body: request });
      onDone();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not record that debt.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="mt-4 rounded-xl border border-border bg-surface p-4">
      <p className="text-sm font-semibold text-text">Record a debt</p>
      <div className="mt-2 grid grid-cols-1 gap-2 sm:grid-cols-3">
        <TextField label="Lender" value={lenderName} onChange={(e) => setLenderName(e.target.value)} />
        <TextField label="Amount" type="number" min="0" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
        <TextField label="Due (optional)" type="date" value={dueOn} onChange={(e) => setDueOn(e.target.value)} />
      </div>
      {error && <p className="mt-2 text-sm text-danger">{error}</p>}
      <div className="mt-3 flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="button" onClick={handleSubmit} disabled={isSubmitting}>
          {isSubmitting ? "Saving…" : "Record debt"}
        </Button>
      </div>
    </div>
  );
}

// --- Promises ----------------------------------------------------------------

function PromisesPanel({
  accessToken,
  categories,
  currencyCode,
  locale,
}: {
  accessToken: string | null;
  categories: CategoryBalance[];
  currencyCode: string;
  locale: string;
}) {
  const [promises, setPromises] = useState<MoneyPromise[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [redeemingId, setRedeemingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  function load() {
    if (!accessToken) {
      return;
    }
    apiFetch<MoneyPromise[]>("/promises", { token: accessToken })
      .then((result) => {
        setPromises(result);
        setError(null);
      })
      .catch((err) => setError(err instanceof ApiError ? err.message : "Could not load promises."));
  }

  useEffect(load, [accessToken]);

  return (
    <div>
      <div className="flex justify-end">
        <Button type="button" onClick={() => setShowForm((v) => !v)}>
          {showForm ? "Cancel" : "Make a promise"}
        </Button>
      </div>

      {error && <p className="mt-3 rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

      {showForm && (
        <NewPromiseForm
          accessToken={accessToken}
          onDone={() => {
            setShowForm(false);
            load();
          }}
          onCancel={() => setShowForm(false)}
        />
      )}

      <div className="mt-4 flex flex-col gap-3">
        {promises.length === 0 && !showForm && <p className="text-sm text-muted">No promises made yet.</p>}
        {promises.map((promise) => (
          <div key={promise.id} className="rounded-xl border border-border bg-surface p-4">
            <div className="flex items-center justify-between">
              <span className="font-semibold text-text">{promise.personName}</span>
              <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${STATUS_STYLES[promise.status]}`}>{promise.status}</span>
            </div>
            <p className="mt-1 text-sm text-muted">
              {formatMoney(promise.amount, currencyCode, locale)} promised on {promise.madeOn}
            </p>
            {promise.outstanding > 0 && (
              <p className="mt-1 text-sm font-medium text-text">{formatMoney(promise.outstanding, currencyCode, locale)} still owed</p>
            )}

            {promise.redemptions.length > 0 && (
              <ul className="mt-2 flex flex-col gap-1 text-xs text-muted">
                {promise.redemptions.map((r) => (
                  <li key={r.id}>
                    {formatMoney(r.amount, currencyCode, locale)} redeemed {r.redeemedOn} from {accountLabel(r.sourceAccount, categories)}
                  </li>
                ))}
              </ul>
            )}

            {promise.status !== "Redeemed" && promise.status !== "Cancelled" && (
              <div className="mt-3">
                <Button
                  type="button"
                  variant="secondary"
                  className="h-8 text-xs"
                  onClick={() => setRedeemingId(redeemingId === promise.id ? null : promise.id)}
                >
                  Redeem
                </Button>
              </div>
            )}

            {redeemingId === promise.id && (
              <RedemptionForm
                categories={categories}
                onCancel={() => setRedeemingId(null)}
                onSubmit={async (amount, occurredOn, account) => {
                  const request: CreatePromiseRedemptionRequest = {
                    amount,
                    redeemedOn: occurredOn,
                    sourceAccount: account,
                    note: null,
                  };
                  await apiFetch(`/promises/${promise.id}/redemptions`, { method: "POST", token: accessToken, body: request });
                  setRedeemingId(null);
                  load();
                }}
              />
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

function RedemptionForm({
  categories,
  onCancel,
  onSubmit,
}: {
  categories: CategoryBalance[];
  onCancel: () => void;
  onSubmit: (amount: number, occurredOn: string, account: AccountRefInput) => Promise<void>;
}) {
  const [amount, setAmount] = useState("");
  const [account, setAccount] = useState<AccountRefInput>({ kind: "FlexiblePool", categoryId: null });
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit() {
    if (!amount || Number(amount) <= 0) {
      setError("Enter a valid amount.");
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      await onSubmit(Number(amount), new Date().toISOString().slice(0, 10), account);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not redeem that.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="mt-3 rounded-lg bg-surface-2 p-3">
      <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
        <TextField label="Amount" type="number" min="0" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
        <AccountRefPicker categories={categories} value={account} onChange={(v) => v && setAccount(v)} allowNone={false} />
      </div>
      {error && <p className="mt-2 text-xs text-danger">{error}</p>}
      <div className="mt-3 flex justify-end gap-2">
        <Button type="button" variant="secondary" className="h-8 text-xs" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="button" className="h-8 text-xs" onClick={handleSubmit} disabled={isSubmitting}>
          {isSubmitting ? "Saving…" : "Redeem"}
        </Button>
      </div>
    </div>
  );
}

function NewPromiseForm({ accessToken, onDone, onCancel }: { accessToken: string | null; onDone: () => void; onCancel: () => void }) {
  const [personName, setPersonName] = useState("");
  const [amount, setAmount] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit() {
    if (!personName.trim() || !amount || Number(amount) <= 0) {
      setError("Enter who you promised and a valid amount.");
      return;
    }

    setIsSubmitting(true);
    setError(null);

    const request: CreatePromiseRequest = {
      personName: personName.trim(),
      amount: Number(amount),
      note: null,
      madeOn: new Date().toISOString().slice(0, 10),
    };

    try {
      await apiFetch("/promises", { method: "POST", token: accessToken, body: request });
      onDone();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not record that promise.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="mt-4 rounded-xl border border-border bg-surface p-4">
      <p className="text-sm font-semibold text-text">Make a promise</p>
      <div className="mt-2 grid grid-cols-1 gap-2 sm:grid-cols-2">
        <TextField label="Person" value={personName} onChange={(e) => setPersonName(e.target.value)} />
        <TextField label="Amount" type="number" min="0" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
      </div>
      {error && <p className="mt-2 text-sm text-danger">{error}</p>}
      <div className="mt-3 flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="button" onClick={handleSubmit} disabled={isSubmitting}>
          {isSubmitting ? "Saving…" : "Make promise"}
        </Button>
      </div>
    </div>
  );
}
