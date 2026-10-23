import Link from "next/link";
import { type ComponentProps, type ReactNode } from "react";

export function Container({ children, className, ...rest }: ComponentProps<"div">) {
  return (
    <div className={`mx-auto w-full max-w-7xl px-4 lg:px-8 ${className ?? ""}`} {...rest}>
      {children}
    </div>
  );
}

export function Section({
  title,
  description,
  action,
  children,
  id,
}: {
  title: string;
  description?: string;
  action?: { href: string; label: string };
  children: ReactNode;
  id?: string;
}) {
  const headingId = `${id ?? title.toLowerCase().replace(/[^a-z0-9]+/g, "-")}-heading`;
  return (
    <section aria-labelledby={headingId} className="flex flex-col gap-5">
      <div className="flex items-end justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h2 id={headingId} className="text-text text-2xl font-bold sm:text-3xl">
            {title}
          </h2>
          {description && <p className="text-text-muted">{description}</p>}
        </div>
        {action && (
          <Link
            href={action.href}
            className="text-accent-text shrink-0 text-sm font-semibold underline-offset-4 hover:underline"
          >
            {action.label}
          </Link>
        )}
      </div>
      {children}
    </section>
  );
}

/** Server-safe failure notice for a section whose data did not load. */
export function SectionError({ message, traceId }: { message: string; traceId?: string }) {
  return (
    <div role="alert" className="bg-danger-subtle text-danger-text rounded-xl p-4 text-sm">
      <p>{message}</p>
      {traceId && <p className="mt-1 font-mono text-xs opacity-80">Reference: {traceId}</p>}
    </div>
  );
}
