import { SectionAction, SectionHeader } from "@fresh/ui";
import { ArrowRight } from "lucide-react";
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
  script,
  description,
  action,
  children,
  id,
}: {
  title: string;
  /** A handwritten word after the title (D89), e.g. "Fresh this" + "morning"; sparks follow. */
  script?: string;
  description?: string;
  action?: { href: string; label: string };
  children: ReactNode;
  id?: string;
}) {
  const headingId = `${
    id ??
    `${title} ${script ?? ""}`
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
  }-heading`;
  return (
    <section aria-labelledby={headingId} id={id} className="flex scroll-mt-24 flex-col gap-5">
      <SectionHeader
        id={headingId}
        title={title}
        script={script}
        description={description}
        action={
          action && (
            <SectionAction asChild>
              <Link href={action.href}>
                {action.label}
                <ArrowRight aria-hidden="true" />
              </Link>
            </SectionAction>
          )
        }
      />
      {children}
    </section>
  );
}

/** Server-safe failure notice for a section whose data did not load. */
export function SectionError({ message, traceId }: { message: string; traceId?: string }) {
  return (
    <div role="alert" className="bg-danger-subtle text-danger-text rounded-2xl p-4 text-sm">
      <p>{message}</p>
      {traceId && <p className="mt-1 font-mono text-xs opacity-80">Reference: {traceId}</p>}
    </div>
  );
}
