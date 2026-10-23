import { ContentPage, OwnerPlaceholder } from "./content-page";

/**
 * A policy page whose wording is entirely the store owner's. Each section shows an outline of what the owner must
 * cover, inside a visible placeholder, so nothing here can be read as the store's actual terms.
 */
export function PolicyPage({
  title,
  sections,
}: {
  title: string;
  sections: { heading: string; outline: string }[];
}) {
  return (
    <ContentPage
      title={title}
      breadcrumbs={[
        { href: "/", label: "Home" },
        { href: "/support", label: "Help" },
        { label: title },
      ]}
    >
      <OwnerPlaceholder>
        This page is an outline only. The store owner must write and approve the actual policy (with
        legal advice where needed) before launch.
      </OwnerPlaceholder>
      {sections.map((s) => (
        <section key={s.heading} className="flex flex-col gap-2">
          <h2>{s.heading}</h2>
          <p className="text-text-muted italic">[Placeholder] {s.outline}</p>
        </section>
      ))}
    </ContentPage>
  );
}
