"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import type { AdjustableAlertKind, AlertPreference, Notification, UpdateAlertPreferenceInput } from "@natoshare/shared-types";
import { Button } from "@/components/ui/button";
import { apiFetch, ApiError } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";

const SEVERITY_STYLES: Record<string, string> = {
  Info: "bg-info-tint text-info",
  Warning: "bg-warning-tint text-warning",
  Critical: "bg-danger-tint text-danger",
};

const KIND_LABELS: Record<AdjustableAlertKind, { title: string; description: string }> = {
  OverPaceCategory: { title: "Spending faster than planned", description: "Warn when a category is projected to go over budget by month end." },
  OverspendCategory: { title: "Category goes over budget", description: "Warn the moment a category first crosses its funded amount." },
  CategoryInDeficit: { title: "Category stays in deficit", description: "A daily reminder for as long as a category is still over budget." },
  SafeToSpendLow: { title: "Safe-to-spend running low", description: "Warn when what is safe to spend today has dropped a lot." },
  MonthCloseReminder: { title: "Month still open", description: "Remind me if a month is still sitting open a few days after it ended." },
  FixedAccountUnconfirmed: { title: "Fixed account not confirmed", description: "Let me know if a fixed account transfer was never confirmed." },
  CarriedDeficitApplied: { title: "Deficit carried in", description: "Tell me when a new month opens with a deficit carried in from last month." },
  MonthEndSummary: { title: "Month closed summary", description: "A wrap-up of what I spent and saved once a month is closed." },
  DebtDueSoon: { title: "Debt due soon", description: "Warn a few days before something I borrowed is due back." },
  DebtOverdue: { title: "Debt overdue", description: "Let me know the moment something I borrowed is overdue." },
  LoanReturnDueSoon: { title: "Loan return due soon", description: "Warn a few days before money I lent out was expected back." },
  LoanOverdue: { title: "Loan overdue", description: "Let me know the moment money I lent out is overdue." },
  PromiseReminder: { title: "Promise now affordable", description: "Tell me once the Flexible Pool can cover a promise I still owe." },
  RecurringItemDue: { title: "Recurring item due", description: "Remind me when a recurring income or expense comes due." },
};

// The notification centre: everything Natoshare has told the user, and a place to
// turn each kind of alert up, down, or off.
export function NotificationsScreen() {
  const router = useRouter();
  const accessToken = useAuthStore((state) => state.accessToken);

  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [preferences, setPreferences] = useState<AlertPreference[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showPreferences, setShowPreferences] = useState(false);

  useEffect(() => {
    if (!accessToken) {
      return;
    }

    loadAll();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accessToken]);

  async function loadAll() {
    setIsLoading(true);
    try {
      const [notificationsResult, preferencesResult] = await Promise.all([
        apiFetch<Notification[]>("/notifications?pageSize=50", { token: accessToken }),
        apiFetch<AlertPreference[]>("/notifications/preferences", { token: accessToken }),
      ]);
      setNotifications(notificationsResult);
      setPreferences(preferencesResult);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not load your notifications.");
    } finally {
      setIsLoading(false);
    }
  }

  async function markRead(id: string) {
    setNotifications((current) => current.map((n) => (n.id === id ? { ...n, isRead: true } : n)));
    try {
      await apiFetch(`/notifications/${id}/read`, { method: "POST", token: accessToken });
    } catch {
      // Not worth interrupting the user over, the list will show the real state
      // again next time it loads.
    }
  }

  async function markAllRead() {
    setNotifications((current) => current.map((n) => ({ ...n, isRead: true })));
    try {
      await apiFetch("/notifications/read-all", { method: "POST", token: accessToken });
    } catch {
      // Same as above.
    }
  }

  async function updatePreference(kind: AdjustableAlertKind, patch: Partial<AlertPreference>) {
    const current = preferences.find((p) => p.kind === kind);
    const updated: UpdateAlertPreferenceInput = {
      kind,
      enabled: patch.enabled ?? current?.enabled ?? true,
      thresholdPercent: patch.thresholdPercent !== undefined ? patch.thresholdPercent : (current?.thresholdPercent ?? null),
      leadDays: current?.leadDays ?? null,
    };

    setPreferences((prev) => prev.map((p) => (p.kind === kind ? { ...p, ...updated } : p)));

    try {
      await apiFetch<AlertPreference[]>("/notifications/preferences", {
        method: "PATCH",
        token: accessToken,
        body: [updated],
      });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not save that preference.");
      loadAll();
    }
  }

  const unreadCount = notifications.filter((n) => !n.isRead).length;

  return (
    <main className="px-4 py-10 sm:px-6 lg:px-10 xl:px-16 2xl:px-24">
      <button
        type="button"
        onClick={() => router.push("/dashboard")}
        className="-ml-2 rounded-md px-2 py-1 text-sm font-medium text-muted transition-colors hover:bg-surface hover:text-text hover:shadow-sm"
      >
        ← Back
      </button>

      <div className="mt-3 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-2xl font-bold text-text">Notifications</h1>
        <div className="flex flex-wrap gap-2">
          <Button type="button" variant="secondary" onClick={() => setShowPreferences((v) => !v)}>
            {showPreferences ? "Hide settings" : "Alert settings"}
          </Button>
          {unreadCount > 0 && (
            <Button type="button" variant="secondary" onClick={markAllRead}>
              Mark all read
            </Button>
          )}
        </div>
      </div>

      {error && <p className="mt-4 rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

      {showPreferences && (
        <div className="mt-4 rounded-xl border border-border bg-gradient-to-br from-surface to-primary-tint p-4">
          <p className="text-sm font-semibold text-text">Alert settings</p>
          <div className="mt-3 flex flex-col gap-4">
            {preferences.map((preference) => {
              const label = KIND_LABELS[preference.kind];
              return (
                <div key={preference.kind} className="flex items-start justify-between gap-4">
                  <div>
                    <p className="text-sm font-medium text-text">{label?.title ?? preference.kind}</p>
                    <p className="text-xs text-muted">{label?.description}</p>
                  </div>
                  <button
                    type="button"
                    onClick={() => updatePreference(preference.kind, { enabled: !preference.enabled })}
                    className={`relative h-[26px] w-[44px] shrink-0 rounded-full transition-colors ${
                      preference.enabled ? "bg-primary" : "bg-surface-2"
                    }`}
                    aria-pressed={preference.enabled}
                    aria-label={`Toggle ${label?.title ?? preference.kind}`}
                  >
                    <span
                      className={`absolute top-[3px] h-5 w-5 rounded-full bg-white shadow-sm transition-transform ${
                        preference.enabled ? "translate-x-[21px]" : "translate-x-[3px]"
                      }`}
                    />
                  </button>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {isLoading ? (
        <p className="mt-6 text-sm text-muted">Loading…</p>
      ) : notifications.length === 0 ? (
        <p className="mt-6 text-sm text-muted">Nothing here yet.</p>
      ) : (
        <div className="mt-6 flex flex-col gap-2">
          {notifications.map((notification) => (
            <button
              key={notification.id}
              type="button"
              onClick={() => !notification.isRead && markRead(notification.id)}
              className={`w-full rounded-xl border p-4 text-left transition-colors ${
                notification.isRead
                  ? "border-border bg-gradient-to-br from-surface to-surface-2"
                  : "border-primary-tint bg-primary-tint/40"
              }`}
            >
              <div className="flex items-center justify-between gap-3">
                <p className="text-sm font-semibold text-text">{notification.title}</p>
                <span className={`shrink-0 rounded-full px-2 py-0.5 text-[11px] font-semibold ${SEVERITY_STYLES[notification.severity]}`}>
                  {notification.severity}
                </span>
              </div>
              <p className="mt-1 text-sm text-muted">{notification.body}</p>
            </button>
          ))}
        </div>
      )}
    </main>
  );
}
