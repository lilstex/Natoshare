import Image from "next/image";
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

// The card every auth screen (login, signup, forgot/reset password) sits in.
// Keeping this in one place means all four screens automatically stay looking alike.
//
// Below "md" (see docs/06-design-system.md section 4.1) it is just the centred form,
// same as before. From "md" up, a graphic panel (public/images/auth-graphic.svg)
// takes the other half of the screen, per docs/feedback.md Phase C.
export function AuthCard({ eyebrow, title, description, children, footer }: AuthCardProps) {
  return (
    <main className="flex min-h-screen">
      <div className="relative hidden w-1/2 shrink-0 md:block">
        <Image
          src="/images/auth-graphic.svg"
          alt="Natoshare splits your income into categories automatically"
          fill
          priority
          className="object-cover"
        />
      </div>

      <div className="flex flex-1 flex-col items-center justify-center gap-6 bg-gradient-to-br from-bg to-surface-2 px-4 py-16 sm:px-6">
        <Link href="/" className="flex items-center gap-2.5">
          <BrandMark size={30} />
          <span className="font-display text-lg font-semibold text-text">Natoshare</span>
        </Link>

        <div className="fade-in-up w-full max-w-md rounded-[26px] border border-border bg-gradient-to-br from-surface to-primary-tint p-8 shadow-lg">
          <p className="text-xs font-semibold tracking-wide text-subtle uppercase">{eyebrow}</p>
          <h1 className="mt-1.5 text-2xl font-bold text-text">{title}</h1>
          <p className="mt-1 text-sm text-muted">{description}</p>

          <div className="mt-6">{children}</div>
        </div>

        <p className="text-sm text-muted">{footer}</p>
      </div>
    </main>
  );
}
