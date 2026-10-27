import { Heading, Script } from "@fresh/ui";
import { ArrowRight } from "lucide-react";
import Link from "next/link";
import { type ComponentProps, type ReactNode } from "react";
import { Sparks } from "./theme/marks";

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
      <div className="flex items-end justify-between gap-4">
        <div className="flex flex-col gap-1">
          <Heading id={headingId} level={2} size="section" className="text-text">
            {title}
            {script && (
              <>
                {" "}
                <Script tone="accent">{script}</Script>
              </>
            )}
            <Sparks className="text-accent-coral ml-1 w-6 -translate-y-3 sm:w-7" />
          </Heading>
          {description && <p className="text-text-muted">{description}</p>}
        </div>
        {action && (
          <Link
            href={action.href}
            className="border-primary text-primary hover:bg-primary hover:text-on-primary inline-flex shrink-0 items-center gap-1 rounded-full border-[1.5px] px-4 py-1.5 text-sm font-bold"
          >
            {action.label}
            <ArrowRight aria-hidden="true" className="size-4" />
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
    <div role="alert" className="bg-danger-subtle text-danger-text rounded-2xl p-4 text-sm">
      <p>{message}</p>
      {traceId && <p className="mt-1 font-mono text-xs opacity-80">Reference: {traceId}</p>}
    </div>
  );
}
