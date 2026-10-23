import { TriangleAlert } from "lucide-react";
import { type ReactNode } from "react";
import { Breadcrumbs } from "./breadcrumbs";
import { Container } from "./section";

/**
 * Marks text the store owner must write before launch. Visible on purpose so placeholder policy text can never be
 * mistaken for the real terms. Search the code for `<OwnerPlaceholder` to find every instance.
 */
export function OwnerPlaceholder({ children }: { children?: ReactNode }) {
  return (
    <div
      role="note"
      className="border-highlight bg-highlight-subtle text-highlight-text flex gap-3 rounded-xl border border-dashed p-4 text-sm"
    >
      <TriangleAlert aria-hidden="true" className="mt-0.5 size-5 shrink-0" />
      <div className="flex flex-col gap-1">
        <p className="font-semibold">Placeholder text: to be replaced by the store owner.</p>
        {children && <p>{children}</p>}
      </div>
    </div>
  );
}

export function ContentPage({
  title,
  intro,
  breadcrumbs,
  children,
}: {
  title: string;
  intro?: ReactNode;
  breadcrumbs: { href?: string; label: string }[];
  children: ReactNode;
}) {
  return (
    <Container className="py-10">
      <div className="flex max-w-3xl flex-col gap-6">
        <Breadcrumbs items={breadcrumbs} />
        <header className="flex flex-col gap-2">
          <h1 className="text-text text-3xl font-bold sm:text-4xl">{title}</h1>
          {intro && <p className="text-text-muted text-lg">{intro}</p>}
        </header>
        <div className="text-text flex flex-col gap-4 leading-7 [&_h2]:mt-4 [&_h2]:text-xl [&_h2]:font-semibold [&_li]:ml-5 [&_li]:list-disc">
          {children}
        </div>
      </div>
    </Container>
  );
}
