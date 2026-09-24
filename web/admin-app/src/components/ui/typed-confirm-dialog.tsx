"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";

type TypedConfirmDialogProps = {
  title: string;
  description: string;
  // What the admin has to type back, for example the account's own email. Shown in
  // the dialog so it is never a guessing game, docs/04-admin-app.md section 4 only
  // asks that this be a deliberate action, not that it be a secret.
  confirmText: string;
  confirmLabel?: string;
  isBusy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
};

// The typed-confirm pattern every destructive admin action uses (hard-deleting an
// account is the only one today), modelled on GitHub's "type the repo name to
// delete it". Required from day one per docs/04-admin-app.md section 4, unlike the
// optional "recent re-auth" step which is explicit backlog.
export function TypedConfirmDialog({
  title,
  description,
  confirmText,
  confirmLabel = "Confirm",
  isBusy = false,
  onConfirm,
  onCancel,
}: TypedConfirmDialogProps) {
  const [typed, setTyped] = useState("");
  const matches = typed.trim() === confirmText;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
      <div className="w-full max-w-sm rounded-2xl border border-border bg-surface p-6 shadow-[var(--nato-shadow-lg)]">
        <h2 className="text-lg font-bold text-text">{title}</h2>
        <p className="mt-1.5 text-sm text-muted">{description}</p>

        <p className="mt-4 text-sm text-muted">
          Type <span className="font-mono font-semibold text-text">{confirmText}</span> to confirm.
        </p>
        <div className="mt-2">
          <TextField
            id="typed-confirm"
            label=""
            value={typed}
            onChange={(e) => setTyped(e.target.value)}
            autoComplete="off"
          />
        </div>

        <div className="mt-5 flex justify-end gap-2.5">
          <button
            type="button"
            onClick={onCancel}
            className="inline-flex h-11 items-center justify-center rounded-full px-5 text-sm font-semibold text-muted hover:bg-surface-2"
          >
            Cancel
          </button>
          <Button
            type="button"
            onClick={onConfirm}
            disabled={!matches || isBusy}
            variant="danger"
          >
            {isBusy ? "Working…" : confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}
