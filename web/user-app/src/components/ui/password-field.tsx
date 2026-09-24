"use client";

import { useState } from "react";
import type { InputHTMLAttributes } from "react";
import { EyeIcon, EyeOffIcon } from "@/components/ui/icons";
import { TextField } from "@/components/ui/text-field";

type PasswordFieldProps = Omit<InputHTMLAttributes<HTMLInputElement>, "type"> & {
  label: string;
};

// A password TextField with an eye icon to show or hide what was typed (see
// docs/feedback.md Phase A, item 1). Every password field in the app should use
// this instead of TextField directly, so the toggle only has to be built once.
export function PasswordField({ label, id, ...props }: PasswordFieldProps) {
  const [isVisible, setIsVisible] = useState(false);

  return (
    <TextField
      id={id}
      label={label}
      type={isVisible ? "text" : "password"}
      trailing={
        <button
          type="button"
          onClick={() => setIsVisible((visible) => !visible)}
          aria-label={isVisible ? "Hide password" : "Show password"}
          className="flex h-8 w-8 items-center justify-center rounded-full text-subtle hover:bg-surface-2 hover:text-text focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-tint"
        >
          {isVisible ? <EyeOffIcon /> : <EyeIcon />}
        </button>
      }
      {...props}
    />
  );
}
