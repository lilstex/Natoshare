"use client";

import { useEffect, useState } from "react";
import type { AdminFeatureFlag, SystemSetting } from "@natoshare/shared-types";
import { Card } from "@/components/ui/card";
import { TextField } from "@/components/ui/text-field";
import { apiFetch, ApiError } from "@/lib/api-client";
import { useAdminAuthStore } from "@/store/auth-store";

// On/off switches and small tunable numbers, docs/04-admin-app.md section 2.6.
export function SettingsScreen() {
  const accessToken = useAdminAuthStore((state) => state.accessToken);
  const [flags, setFlags] = useState<AdminFeatureFlag[]>([]);
  const [settings, setSettings] = useState<SystemSetting[]>([]);
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  function load() {
    if (!accessToken) return;
    apiFetch<AdminFeatureFlag[]>("/admin/feature-flags", { token: accessToken }).then(setFlags).catch(() => {});
    apiFetch<SystemSetting[]>("/admin/settings", { token: accessToken })
      .then((data) => {
        setSettings(data);
        setDrafts(Object.fromEntries(data.map((s) => [s.key, s.value])));
      })
      .catch((err) => setError(err instanceof ApiError ? err.message : "Could not load settings."));
  }

  useEffect(load, [accessToken]);

  async function toggleFlag(key: string, enabled: boolean) {
    if (!accessToken) return;
    try {
      await apiFetch(`/admin/feature-flags/${key}`, { method: "PATCH", token: accessToken, body: { enabled } });
      setNotice(`${key} ${enabled ? "enabled" : "disabled"}.`);
      load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not update that flag.");
    }
  }

  async function saveSetting(key: string) {
    if (!accessToken) return;
    try {
      await apiFetch(`/admin/settings/${key}`, { method: "PATCH", token: accessToken, body: { value: drafts[key] } });
      setNotice(`${key} updated.`);
      load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not update that setting.");
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold text-text">Settings</h1>
        <p className="mt-1 text-sm text-muted">Feature flags and small tunable numbers, no deploy needed.</p>
      </div>

      {notice && <p className="rounded-xl bg-success-tint px-3.5 py-2.5 text-sm text-success">{notice}</p>}
      {error && <p className="rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

      <Card>
        <h2 className="font-display text-base font-bold text-text">Feature flags</h2>
        <div className="mt-3 flex flex-col divide-y divide-border">
          {flags.map((flag) => (
            <label key={flag.key} className="flex items-center justify-between py-2.5 text-sm">
              <span className="font-medium text-text">{flag.key}</span>
              <input type="checkbox" checked={flag.enabled} onChange={(e) => toggleFlag(flag.key, e.target.checked)} />
            </label>
          ))}
          {flags.length === 0 && <p className="py-2 text-sm text-muted">No feature flags yet.</p>}
        </div>
      </Card>

      <Card>
        <h2 className="font-display text-base font-bold text-text">System settings</h2>
        <div className="mt-3 flex flex-col gap-3">
          {settings.map((setting) => (
            <div key={setting.key} className="flex items-end gap-3">
              <div className="flex-1">
                <TextField
                  id={`setting-${setting.key}`}
                  label={setting.key}
                  value={drafts[setting.key] ?? ""}
                  onChange={(e) => setDrafts({ ...drafts, [setting.key]: e.target.value })}
                />
              </div>
              <button
                type="button"
                onClick={() => saveSetting(setting.key)}
                className="h-11 rounded-full border border-border px-4 text-sm font-medium text-text hover:bg-surface-2"
              >
                Save
              </button>
            </div>
          ))}
          {settings.length === 0 && <p className="text-sm text-muted">No settings yet.</p>}
        </div>
      </Card>
    </div>
  );
}
