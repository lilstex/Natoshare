"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import type { AdminUserListResult } from "@natoshare/shared-types";
import { Badge, statusTone } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { TextField } from "@/components/ui/text-field";
import { apiFetch, ApiError } from "@/lib/api-client";
import { useAdminAuthStore } from "@/store/auth-store";

// Read-first, docs/04-admin-app.md section 2.2: this list is where the team finds
// an account before doing anything to it, search and filters are the whole point.
export function UsersScreen() {
  const accessToken = useAdminAuthStore((state) => state.accessToken);
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState("");
  const [result, setResult] = useState<AdminUserListResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);

  useEffect(() => {
    if (!accessToken) return;

    const params = new URLSearchParams({ page: String(page), pageSize: "25" });
    if (query) params.set("q", query);
    if (status) params.set("status", status);

    apiFetch<AdminUserListResult>(`/admin/users?${params.toString()}`, { token: accessToken })
      .then((data) => {
        setResult(data);
        setError(null);
      })
      .catch((err) => setError(err instanceof ApiError ? err.message : "Could not load users."));
  }, [accessToken, query, status, page]);

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold text-text">Users</h1>
        <p className="mt-1 text-sm text-muted">{result?.totalCount ?? 0} accounts.</p>
      </div>

      <Card className="flex flex-wrap items-end gap-4">
        <div className="w-64">
          <TextField
            id="search"
            label="Search"
            placeholder="Email or name"
            value={query}
            onChange={(e) => {
              setPage(1);
              setQuery(e.target.value);
            }}
          />
        </div>
        <label className="flex flex-col gap-1.5 text-left">
          <span className="text-sm font-medium text-muted">Status</span>
          <select
            className="h-11 rounded-xl border border-border-strong bg-surface px-3.5 text-[15px] text-text outline-none focus:border-primary focus:ring-4 focus:ring-primary-tint"
            value={status}
            onChange={(e) => {
              setPage(1);
              setStatus(e.target.value);
            }}
          >
            <option value="">Any status</option>
            <option value="Active">Active</option>
            <option value="Suspended">Suspended</option>
            <option value="PendingDeletion">PendingDeletion</option>
          </select>
        </label>
      </Card>

      {error && <p className="rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

      <Card className="overflow-x-auto p-0">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-border text-xs uppercase tracking-wide text-subtle">
            <tr>
              <th className="px-5 py-3">Name</th>
              <th className="px-5 py-3">Email</th>
              <th className="px-5 py-3">Status</th>
              <th className="px-5 py-3">Plan</th>
              <th className="px-5 py-3">Joined</th>
            </tr>
          </thead>
          <tbody>
            {result?.items.map((item) => (
              <tr key={item.id} className="border-b border-border last:border-0 hover:bg-surface-2">
                <td className="px-5 py-3">
                  <Link href={`/users/${item.id}`} className="font-medium text-primary hover:underline">
                    {item.displayName}
                  </Link>
                </td>
                <td className="px-5 py-3 text-muted">{item.email}</td>
                <td className="px-5 py-3">
                  <Badge tone={statusTone(item.status)}>{item.status}</Badge>
                </td>
                <td className="px-5 py-3 text-muted">
                  {item.plan}
                  {item.isTrial ? " (trial)" : ""}
                </td>
                <td className="px-5 py-3 text-muted">{new Date(item.createdAt).toLocaleDateString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>

      {result && result.totalCount > result.pageSize && (
        <div className="flex items-center gap-3">
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => setPage((p) => p - 1)}
            className="rounded-full border border-border px-4 py-2 text-sm font-medium text-text disabled:opacity-40"
          >
            Previous
          </button>
          <span className="text-sm text-muted">Page {page}</span>
          <button
            type="button"
            disabled={page * result.pageSize >= result.totalCount}
            onClick={() => setPage((p) => p + 1)}
            className="rounded-full border border-border px-4 py-2 text-sm font-medium text-text disabled:opacity-40"
          >
            Next
          </button>
        </div>
      )}
    </div>
  );
}
