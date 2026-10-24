import { Tabs, TabsContent, TabsList, TabsTrigger, cn } from "@fresh/ui";
import { Moon, Sun, SunMoon } from "lucide-react";
import { type ReactNode, useEffect, useState } from "react";
import { AdminTablePage } from "./pages/AdminTablePage";
import { ComponentsPage } from "./pages/ComponentsPage";
import { IllustratedPage } from "./pages/IllustratedPage";
import { ProductPage } from "./pages/ProductPage";
import { TokensPage } from "./pages/TokensPage";

type View = "light" | "dark" | "both";

const views: { id: View; label: string; icon: ReactNode }[] = [
  { id: "light", label: "Light", icon: <Sun aria-hidden="true" className="size-4" /> },
  { id: "dark", label: "Dark", icon: <Moon aria-hidden="true" className="size-4" /> },
  { id: "both", label: "Side by side", icon: <SunMoon aria-hidden="true" className="size-4" /> },
];

/** Renders a page once (light or dark) or twice, side by side, each panel scoped with data-theme. */
function Themed({ view, children }: { view: View; children: () => ReactNode }) {
  if (view !== "both") return <>{children()}</>;
  return (
    <div className="grid gap-4 xl:grid-cols-2">
      {(["light", "dark"] as const).map((theme) => (
        <section
          key={theme}
          data-theme={theme}
          aria-label={`${theme} theme`}
          className="border-border bg-background text-text rounded-xl border p-4"
        >
          <p className="text-text-muted mb-4 text-xs font-semibold tracking-wide uppercase">
            {theme}
          </p>
          {children()}
        </section>
      ))}
    </div>
  );
}

export function App() {
  const [view, setView] = useState<View>("both");

  useEffect(() => {
    // Portals (dialogs, menus, toasts) follow the document theme.
    document.documentElement.dataset.theme = view === "dark" ? "dark" : "light";
  }, [view]);

  return (
    <div className="bg-background text-text min-h-dvh">
      <header className="border-border bg-surface border-b">
        <div className="mx-auto flex max-w-[1600px] flex-wrap items-center justify-between gap-4 px-4 py-4">
          <div className="flex items-center gap-3">
            <span
              aria-hidden="true"
              className="bg-primary font-display text-on-primary flex size-9 items-center justify-center rounded-lg text-lg font-bold"
            >
              F
            </span>
            <div>
              <h1 className="font-display text-lg leading-6 font-semibold">
                Fresh Orbit Playground
              </h1>
              <p className="text-text-muted text-xs">
                Design tokens, @fresh/ui components and illustrations
              </p>
            </div>
          </div>
          <div
            role="radiogroup"
            aria-label="Theme"
            className="border-border bg-muted inline-flex rounded-lg border p-1"
          >
            {views.map((v) => (
              <button
                key={v.id}
                type="button"
                role="radio"
                aria-checked={view === v.id}
                onClick={() => setView(v.id)}
                className={cn(
                  "text-text-muted inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-md px-3 text-sm font-medium",
                  "focus-visible:outline-ring focus-visible:outline-2 focus-visible:outline-offset-2",
                  view === v.id && "bg-surface text-text shadow-xs",
                )}
              >
                {v.icon}
                {v.label}
              </button>
            ))}
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-[1600px] px-4 py-6">
        <Tabs defaultValue="components">
          <TabsList aria-label="Playground sections" className="mb-2 w-full overflow-x-auto">
            <TabsTrigger value="components">Components</TabsTrigger>
            <TabsTrigger value="illustrated">Illustrated</TabsTrigger>
            <TabsTrigger value="tokens">Tokens</TabsTrigger>
            <TabsTrigger value="admin">Admin table</TabsTrigger>
            <TabsTrigger value="product">Product card</TabsTrigger>
          </TabsList>
          <TabsContent value="components">
            <Themed view={view}>{() => <ComponentsPage />}</Themed>
          </TabsContent>
          <TabsContent value="illustrated">
            <Themed view={view}>{() => <IllustratedPage />}</Themed>
          </TabsContent>
          <TabsContent value="tokens">
            <TokensPage />
          </TabsContent>
          <TabsContent value="admin">
            <Themed view={view}>{() => <AdminTablePage />}</Themed>
          </TabsContent>
          <TabsContent value="product">
            <Themed view={view}>{() => <ProductPage />}</Themed>
          </TabsContent>
        </Tabs>
      </main>
    </div>
  );
}
