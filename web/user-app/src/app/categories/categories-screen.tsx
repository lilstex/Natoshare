"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import type { Category, CategoryAllocationInput, CategoryKind } from "@natoshare/shared-types";
import { Button } from "@/components/ui/button";
import { apiFetch, ApiError } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";

type MeResponse = {
  user: { currencyCode: string; locale: string };
};

// One category being archived needs a brand new split for the rest, this holds what
// the user has typed for that while the "archive" panel for a category is open.
type ArchiveDraft = {
  rows: { categoryId: string; name: string; percentage: number }[];
};

// Lets a user see, add to, and retire the categories their income gets split across.
// Archiving a category is not a simple delete, the money it used to get has to go
// somewhere, so archiving always asks for a new split for what is left.
export function CategoriesScreen() {
  const router = useRouter();
  const accessToken = useAuthStore((state) => state.accessToken);
  const [accountLocale, setAccountLocale] = useState({ locale: "en", currencyCode: "USD" });

  const [categories, setCategories] = useState<Category[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [newName, setNewName] = useState("");
  const [newKind, setNewKind] = useState<CategoryKind>("Standard");
  const [isCreating, setIsCreating] = useState(false);

  const [archivingId, setArchivingId] = useState<string | null>(null);
  const [archiveDraft, setArchiveDraft] = useState<ArchiveDraft | null>(null);
  const [isArchiving, setIsArchiving] = useState(false);

  useEffect(() => {
    if (!accessToken) {
      return;
    }

    loadCategories();
    apiFetch<MeResponse>("/me", { token: accessToken })
      .then((me) => setAccountLocale({ locale: me.user.locale, currencyCode: me.user.currencyCode }))
      .catch(() => {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accessToken]);

  async function loadCategories() {
    setIsLoading(true);
    try {
      const result = await apiFetch<Category[]>("/categories?includeArchived=true", { token: accessToken });
      setCategories(result);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not load your categories.");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleCreate() {
    if (!newName.trim()) {
      return;
    }

    setIsCreating(true);
    setError(null);

    try {
      await apiFetch("/categories", {
        method: "POST",
        token: accessToken,
        body: { name: newName.trim(), kind: newKind, externalAccountLabel: null, subCategories: null },
      });
      setNewName("");
      await loadCategories();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not create that category.");
    } finally {
      setIsCreating(false);
    }
  }

  function startArchiving(category: Category) {
    const remaining = categories.filter((c) => c.id !== category.id && !c.isArchived);
    // Spreads the archived category's share evenly across whatever is left, as a
    // starting point, the user can still change the numbers before confirming.
    const evenShare = remaining.length > 0 ? Number((100 / remaining.length).toFixed(2)) : 0;

    setArchivingId(category.id);
    setArchiveDraft({
      rows: remaining.map((c) => ({ categoryId: c.id, name: c.name, percentage: evenShare })),
    });
  }

  function updateArchiveRow(categoryId: string, percentage: number) {
    setArchiveDraft((current) =>
      current
        ? { rows: current.rows.map((r) => (r.categoryId === categoryId ? { ...r, percentage } : r)) }
        : current,
    );
  }

  async function confirmArchive() {
    if (!archivingId || !archiveDraft) {
      return;
    }

    const total = archiveDraft.rows.reduce((sum, r) => sum + r.percentage, 0);
    if (total !== 100) {
      setError(`The remaining categories add up to ${total}%, they need to add up to exactly 100%.`);
      return;
    }

    setIsArchiving(true);
    setError(null);

    const newAllocations: CategoryAllocationInput[] = archiveDraft.rows.map((r) => ({
      categoryId: r.categoryId,
      percentage: r.percentage,
    }));

    try {
      await apiFetch(`/categories/${archivingId}/archive`, {
        method: "POST",
        token: accessToken,
        body: { newAllocations, effectiveFromMonth: null },
      });
      setArchivingId(null);
      setArchiveDraft(null);
      await loadCategories();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not archive that category.");
    } finally {
      setIsArchiving(false);
    }
  }

  const activeCategories = categories.filter((c) => !c.isArchived);
  const archivedCategories = categories.filter((c) => c.isArchived);

  return (
    <main className="mx-auto max-w-2xl px-4 py-10">
      <button type="button" onClick={() => router.push("/dashboard")} className="text-sm font-medium text-muted hover:text-text">
        ← Back
      </button>

      <h1 className="mt-3 text-2xl font-bold text-text">Your categories</h1>
      <p className="mt-1 text-sm text-muted">
        This is where Rent, Feeding and everything else your income splits into lives. Archiving one moves its
        share to a new split, it does not delete your history.
      </p>

      {error && <p className="mt-4 rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

      {isLoading ? (
        <p className="mt-6 text-sm text-muted">Loading…</p>
      ) : (
        <div className="mt-6 flex flex-col gap-3">
          {activeCategories.map((category) => (
            <div key={category.id} className="rounded-xl border border-border bg-surface p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="font-semibold text-text">{category.name}</p>
                  <p className="text-xs text-muted">
                    {category.kind === "FixedAccount" ? "Fixed account" : "Standard"}
                    {category.currentPercentage !== null ? ` · ${category.currentPercentage}%` : " · not split yet"}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => startArchiving(category)}
                  className="text-sm font-medium text-muted hover:text-danger"
                >
                  Archive
                </button>
              </div>

              {archivingId === category.id && archiveDraft && (
                <div className="mt-3 rounded-lg bg-surface-2 p-3">
                  <p className="text-xs font-semibold text-text">
                    Archiving {category.name}, spread its share across what is left:
                  </p>
                  <div className="mt-2 flex flex-col gap-1.5">
                    {archiveDraft.rows.map((row) => (
                      <div key={row.categoryId} className="flex items-center gap-2 text-sm">
                        <span className="flex-1 text-muted">{row.name}</span>
                        <input
                          type="number"
                          min="0"
                          max="100"
                          step="0.01"
                          value={row.percentage}
                          onChange={(e) => updateArchiveRow(row.categoryId, Number(e.target.value))}
                          className="h-8 w-16 rounded border border-border-strong bg-surface px-2 text-right text-sm outline-none focus:border-primary"
                        />
                        <span className="text-subtle">%</span>
                      </div>
                    ))}
                  </div>
                  <div className="mt-3 flex justify-end gap-2">
                    <Button
                      type="button"
                      variant="secondary"
                      onClick={() => {
                        setArchivingId(null);
                        setArchiveDraft(null);
                      }}
                    >
                      Cancel
                    </Button>
                    <Button type="button" onClick={confirmArchive} disabled={isArchiving}>
                      {isArchiving ? "Archiving…" : "Confirm archive"}
                    </Button>
                  </div>
                </div>
              )}
            </div>
          ))}

          <div className="rounded-xl border border-dashed border-border-strong p-4">
            <p className="text-sm font-semibold text-text">Add a category</p>
            <div className="mt-2 flex gap-2">
              <input
                value={newName}
                onChange={(e) => setNewName(e.target.value)}
                placeholder="Category name"
                className="h-10 flex-1 rounded-lg border border-border-strong bg-surface px-3 text-sm text-text outline-none focus:border-primary"
              />
              <select
                value={newKind}
                onChange={(e) => setNewKind(e.target.value as CategoryKind)}
                className="h-10 rounded-lg border border-border-strong bg-surface px-2 text-sm text-text outline-none focus:border-primary"
              >
                <option value="Standard">Standard</option>
                <option value="FixedAccount">Fixed account</option>
              </select>
              <Button type="button" onClick={handleCreate} disabled={isCreating}>
                Add
              </Button>
            </div>
            <p className="mt-1.5 text-xs text-subtle">
              A new category starts with no percentage, give it a share by setting up a new split.
            </p>
          </div>

          {archivedCategories.length > 0 && (
            <div className="mt-4">
              <p className="text-sm font-semibold text-subtle">Archived</p>
              <div className="mt-2 flex flex-col gap-2">
                {archivedCategories.map((category) => (
                  <div key={category.id} className="rounded-xl border border-border bg-surface-2 px-4 py-2.5 opacity-70">
                    <p className="text-sm text-muted">{category.name}</p>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      <p className="mt-6 text-xs text-subtle">
        Amounts on this page use your account&apos;s currency ({accountLocale.currencyCode}) and locale (
        {accountLocale.locale}).
      </p>
    </main>
  );
}
