"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import type {
  BalancesResult,
  CloseDeficitResolutionInput,
  CloseMonthRequest,
  CloseMonthResult,
  ClosePreview,
  ConfirmFixedAccountRequest,
} from "@natoshare/shared-types";
import { SiteHeader } from "@/components/site-header";
import { Button } from "@/components/ui/button";
import { apiFetch, ApiError } from "@/lib/api-client";
import { formatMoney } from "@/lib/format";
import { useAuthStore } from "@/store/auth-store";

type MeResponse = { user: { currencyCode: string; locale: string } };

function monthName(month: number): string {
  return new Date(2000, month - 1, 1).toLocaleString("en-US", { month: "long" });
}

// The close-month ritual: confirm fixed accounts, see what will roll into savings,
// resolve every open deficit, then close for good. Closing stays disabled until
// every listed deficit has a chosen way to cover it.
export function CloseMonthScreen() {
  const router = useRouter();
  const accessToken = useAuthStore((state) => state.accessToken);

  const [accountLocale, setAccountLocale] = useState({ currencyCode: "USD", locale: "en" });
  const [year, setYear] = useState<number | null>(null);
  const [month, setMonth] = useState<number | null>(null);
  const [preview, setPreview] = useState<ClosePreview | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [alreadyClosed, setAlreadyClosed] = useState(false);

  const [confirmations, setConfirmations] = useState<Record<string, { amount: string; transferredOn: string }>>({});
  const [confirmedIds, setConfirmedIds] = useState<Set<string>>(new Set());
  const [resolutions, setResolutions] = useState<Record<string, { method: string; sourceCategoryId: string | null }>>({});
  const [isClosing, setIsClosing] = useState(false);
  const [result, setResult] = useState<CloseMonthResult | null>(null);

  async function loadPreview() {
    setIsLoading(true);
    setError(null);

    try {
      const balances = await apiFetch<BalancesResult>("/balances", { token: accessToken });
      setYear(balances.month.year);
      setMonth(balances.month.month);

      if (balances.month.status === "Closed") {
        setAlreadyClosed(true);
        return;
      }

      const closePreview = await apiFetch<ClosePreview>(
        `/months/${balances.month.year}/${balances.month.month}/close-preview`,
        { token: accessToken },
      );
      setPreview(closePreview);

      // Every deficit starts with a real choice already made, either the best
      // suggested source or "carry to next month" when nothing has any money to
      // cover it with, so the dropdown's value and what actually gets submitted
      // never disagree with each other.
      const initialResolutions: Record<string, { method: string; sourceCategoryId: string | null }> = {};
      for (const deficit of closePreview.deficits) {
        initialResolutions[deficit.categoryId] =
          deficit.suggestedSources.length > 0
            ? { method: deficit.suggestedSources[0].kind, sourceCategoryId: deficit.suggestedSources[0].categoryId }
            : { method: "NextMonthAllocation", sourceCategoryId: null };
      }
      setResolutions(initialResolutions);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not load the close preview.");
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    if (!accessToken) {
      return;
    }

    apiFetch<MeResponse>("/me", { token: accessToken })
      .then((me) => setAccountLocale({ currencyCode: me.user.currencyCode, locale: me.user.locale }))
      .catch(() => {});

    // Same fetch-on-mount shape used on the dashboard, this is a real network call,
    // the loading state genuinely needs to flip on straight away.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadPreview();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accessToken]);

  async function handleConfirmFixedAccount(categoryId: string) {
    if (!year || !month) {
      return;
    }

    const draft = confirmations[categoryId];
    if (!draft?.amount || !draft.transferredOn) {
      setError("Enter an amount and a date before confirming.");
      return;
    }

    setError(null);

    const request: ConfirmFixedAccountRequest = {
      categoryId,
      amount: Number(draft.amount),
      transferredOn: draft.transferredOn,
    };

    try {
      await apiFetch(`/months/${year}/${month}/confirm-fixed-account`, { method: "POST", token: accessToken, body: request });
      setConfirmedIds((current) => new Set(current).add(categoryId));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not confirm that transfer.");
    }
  }

  const allDeficitsResolved = preview ? preview.deficits.every((d) => resolutions[d.categoryId]?.method) : true;

  async function handleClose() {
    if (!year || !month || !preview) {
      return;
    }

    setIsClosing(true);
    setError(null);

    const deficitResolutions: CloseDeficitResolutionInput[] = preview.deficits.map((d) => ({
      categoryId: d.categoryId,
      amount: d.amount,
      method: resolutions[d.categoryId]!.method as CloseDeficitResolutionInput["method"],
      sourceCategoryId: resolutions[d.categoryId]!.sourceCategoryId,
      note: null,
    }));

    const request: CloseMonthRequest = {
      fixedAccountConfirmations: null,
      deficitResolutions,
      promiseRedemptions: null,
      rebalances: null,
    };

    try {
      const closeResult = await apiFetch<CloseMonthResult>(`/months/${year}/${month}/close`, {
        method: "POST",
        token: accessToken,
        body: request,
      });
      setResult(closeResult);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not close this month.");
    } finally {
      setIsClosing(false);
    }
  }

  return (
    <div>
      <SiteHeader />

      <main className="px-4 pb-16 pt-6 sm:px-6 lg:px-10 xl:px-16 2xl:px-24">
        <button
          type="button"
          onClick={() => router.push("/dashboard")}
          className="-ml-2 rounded-md px-2 py-1 text-sm font-medium text-muted transition-colors hover:bg-surface hover:text-text hover:shadow-sm"
        >
          ← Back
        </button>

        <h1 className="mt-3 text-2xl font-bold text-text">
          {year && month ? `Close ${monthName(month)} ${year}` : "Close month"}
        </h1>

        {error && <p className="mt-4 rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

        {isLoading ? (
          <p className="mt-6 text-sm text-muted">Loading…</p>
        ) : alreadyClosed ? (
          <p className="mt-6 rounded-xl border border-border bg-gradient-to-br from-surface to-primary-tint p-4 text-sm text-muted">
            This month is already closed. Come back once next month has some activity in it.
          </p>
        ) : result ? (
          <div className="mt-6 rounded-xl border border-success bg-success-tint p-5">
            <p className="text-lg font-semibold text-text">{monthName(result.month)} {result.year} is closed 🎉</p>
            <p className="mt-1 text-sm text-muted">
              Saved {formatMoney(result.categories.reduce((sum, c) => sum + (c.savedThisMonth ?? 0), 0), accountLocale.currencyCode, accountLocale.locale)} across every category.
            </p>
            <Button type="button" className="mt-4" onClick={() => router.push("/dashboard")}>
              Back to dashboard
            </Button>
          </div>
        ) : preview ? (
          <div className="mt-6 flex flex-col gap-6">
            {preview.missingIncomeHint && (
              <p className="rounded-xl bg-warning-tint px-3.5 py-2.5 text-sm text-warning">{preview.missingIncomeHint}</p>
            )}

            {preview.fixedAccountsToConfirm.length > 0 && (
              <section className="rounded-xl border border-border bg-gradient-to-br from-surface to-primary-tint p-4">
                <h2 className="text-sm font-semibold text-text">Confirm fixed accounts</h2>
                <p className="mt-1 text-xs text-muted">Say how much actually moved out, and when, for each fixed account category.</p>
                <div className="mt-3 flex flex-col gap-3">
                  {preview.fixedAccountsToConfirm.map((fa) => (
                    <div key={fa.categoryId} className="rounded-lg bg-surface-2 p-3">
                      <div className="flex items-center justify-between">
                        <span className="text-sm font-medium text-text">{fa.name}</span>
                        <span className="text-xs text-muted">Allocated {formatMoney(fa.allocated, accountLocale.currencyCode, accountLocale.locale)}</span>
                      </div>
                      {confirmedIds.has(fa.categoryId) ? (
                        <p className="mt-2 text-xs text-success">Confirmed</p>
                      ) : (
                        <div className="mt-2 flex gap-2">
                          <input
                            type="number"
                            min="0"
                            step="0.01"
                            placeholder="Amount"
                            value={confirmations[fa.categoryId]?.amount ?? String(fa.allocated)}
                            onChange={(e) =>
                              setConfirmations((c) => ({
                                ...c,
                                [fa.categoryId]: { amount: e.target.value, transferredOn: c[fa.categoryId]?.transferredOn ?? "" },
                              }))
                            }
                            className="h-9 w-28 rounded-md border border-border-strong bg-surface px-2 text-sm outline-none focus:border-primary"
                          />
                          <input
                            type="date"
                            value={confirmations[fa.categoryId]?.transferredOn ?? ""}
                            onChange={(e) =>
                              setConfirmations((c) => ({
                                ...c,
                                [fa.categoryId]: { amount: c[fa.categoryId]?.amount ?? String(fa.allocated), transferredOn: e.target.value },
                              }))
                            }
                            className="h-9 flex-1 rounded-md border border-border-strong bg-surface px-2 text-sm outline-none focus:border-primary"
                          />
                          <Button type="button" variant="secondary" className="h-9 text-xs" onClick={() => handleConfirmFixedAccount(fa.categoryId)}>
                            Confirm
                          </Button>
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </section>
            )}

            <section className="rounded-xl border border-border bg-gradient-to-br from-surface to-primary-tint p-4">
              <h2 className="text-sm font-semibold text-text">Rolls into savings</h2>
              {preview.projectedSavings.length === 0 ? (
                <p className="mt-1 text-xs text-muted">Nothing left over to save this month.</p>
              ) : (
                <div className="mt-2 flex flex-col gap-1.5">
                  {preview.projectedSavings.map((p) => (
                    <div key={p.categoryId} className="flex items-center justify-between text-sm">
                      <span className="text-muted">{p.name}</span>
                      <span className="font-semibold text-success">{formatMoney(p.saved, accountLocale.currencyCode, accountLocale.locale)}</span>
                    </div>
                  ))}
                  <div className="mt-1 flex items-center justify-between border-t border-border pt-1.5 text-sm font-semibold">
                    <span>Total</span>
                    <span>{formatMoney(preview.totalSavings, accountLocale.currencyCode, accountLocale.locale)}</span>
                  </div>
                </div>
              )}
            </section>

            {preview.deficits.length > 0 && (
              <section className="rounded-xl border border-danger bg-danger-tint/40 p-4">
                <h2 className="text-sm font-semibold text-text">
                  {preview.deficits.length} {preview.deficits.length === 1 ? "category ended" : "categories ended"} over budget
                </h2>
                <p className="mt-1 text-xs text-muted">Choose how each is covered. This month cannot close until every one is resolved.</p>

                <div className="mt-3 flex flex-col gap-3">
                  {preview.deficits.map((deficit) => (
                    <div key={deficit.categoryId} className="rounded-lg border border-border bg-gradient-to-br from-surface to-primary-tint p-3">
                      <div className="flex items-center justify-between">
                        <span className="text-sm font-medium text-text">{deficit.name}</span>
                        <span className="text-sm font-semibold text-danger">
                          Over by {formatMoney(deficit.amount, accountLocale.currencyCode, accountLocale.locale)}
                        </span>
                      </div>

                      {deficit.suggestedSources.length === 0 ? (
                        <p className="mt-2 text-xs text-muted">
                          Nothing has enough saved up to cover this, choose to carry it to next month instead below.
                        </p>
                      ) : null}

                      <select
                        value={resolutions[deficit.categoryId]?.method ?? "NextMonthAllocation"}
                        onChange={(e) => {
                          const selected = deficit.suggestedSources.find((s) => s.kind === e.target.value);
                          setResolutions((r) => ({
                            ...r,
                            [deficit.categoryId]: { method: e.target.value, sourceCategoryId: selected?.categoryId ?? null },
                          }));
                        }}
                        className="mt-2 h-9 w-full rounded-md border border-border-strong bg-surface px-2 text-sm outline-none focus:border-primary"
                      >
                        {deficit.suggestedSources.map((s, i) => (
                          <option key={i} value={s.kind}>
                            {s.kind === "FlexiblePool"
                              ? `Flexible Pool (${formatMoney(s.availableToUse, accountLocale.currencyCode, accountLocale.locale)} available)`
                              : s.kind === "OwnSavings"
                                ? "This category's own savings"
                                : "Another category's savings"}
                          </option>
                        ))}
                        <option value="NextMonthAllocation">Carry to next month</option>
                      </select>
                      {resolutions[deficit.categoryId]?.method === "NextMonthAllocation" && (
                        <p className="mt-1 text-[11px] text-muted">Next month&apos;s {deficit.name} budget will start lower by this amount.</p>
                      )}
                    </div>
                  ))}
                </div>
              </section>
            )}

            <div className="flex justify-end">
              <Button type="button" onClick={handleClose} disabled={!allDeficitsResolved || isClosing}>
                {isClosing ? "Closing…" : `Close ${year && month ? monthName(month) : "month"}`}
              </Button>
            </div>
            {!allDeficitsResolved && (
              <p className="text-right text-xs text-danger">Close is blocked until every deficit has a cover source.</p>
            )}
          </div>
        ) : null}
      </main>
    </div>
  );
}
