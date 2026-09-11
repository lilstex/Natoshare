import Link from "next/link";
import type { ReactNode } from "react";
import { BrandMark } from "@/components/brand-mark";

type AuthCardProps = {
  eyebrow: string;
  title: string;
  description: string;
  children: ReactNode;
  footer: ReactNode;
};

// The centred card every auth screen (login, signup, forgot/reset password) sits in.
// Keeping this in one place means all four screens automatically stay looking alike.
export function AuthCard({ eyebrow, title, description, children, footer }: AuthCardProps) {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-6 bg-bg px-4 py-16">
      <Link href="/" className="flex items-center gap-2.5">
        <BrandMark size={30} />
        <span className="font-display text-lg font-semibold text-text">Natoshare</span>
      </Link>

      <div className="w-full max-w-md rounded-[26px] border border-border bg-surface p-8 shadow-lg">
        <p className="text-xs font-semibold tracking-wide text-subtle uppercase">{eyebrow}</p>
        <h1 className="mt-1.5 text-2xl font-bold text-text">{title}</h1>
        <p className="mt-1 text-sm text-muted">{description}</p>

        <div className="mt-6">{children}</div>
      </div>

      <p className="text-sm text-muted">{footer}</p>
    </main>
  );
}
