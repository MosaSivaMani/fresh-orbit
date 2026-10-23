import { Menu, PanelLeftClose, PanelLeftOpen } from "lucide-react";
import { type ReactNode, useId, useState } from "react";
import { cn, focusRing } from "../lib/cn";
import { IconButton } from "./button";
import { Sheet, SheetContent } from "./overlay";

export interface NavItem {
  id: string;
  label: string;
  href: string;
  icon?: ReactNode;
  active?: boolean;
  /** Small count or status (e.g. pending orders). */
  badge?: ReactNode;
}

export interface NavSection {
  id: string;
  /** Optional heading above the items. */
  label?: string;
  items: readonly NavItem[];
}

export interface AppShellProps {
  /** Logo / product name for the sidebar and mobile top bar. */
  brand: ReactNode;
  /** Short brand (icon) shown when the sidebar is collapsed. */
  brandCollapsed?: ReactNode;
  navigation: readonly NavSection[];
  /** Top bar content on the left (page title, breadcrumbs, location switcher). */
  topBarStart?: ReactNode;
  /** Top bar content on the right (search, notifications, user menu). */
  topBarEnd?: ReactNode;
  /** Controlled collapse state of the desktop sidebar. */
  collapsed?: boolean;
  defaultCollapsed?: boolean;
  onCollapsedChange?: (collapsed: boolean) => void;
  /** Render links with the app router (Next.js Link, React Router NavLink). Defaults to <a>. */
  renderLink?: (
    item: NavItem,
    props: { className: string; children: ReactNode; onClick?: () => void },
  ) => ReactNode;
  /** Fill the parent instead of the viewport (previews, playground). */
  embedded?: boolean;
  children: ReactNode;
  className?: string;
}

function defaultLink(
  item: NavItem,
  props: { className: string; children: ReactNode; onClick?: () => void },
) {
  return (
    <a
      href={item.href}
      aria-current={item.active ? "page" : undefined}
      className={props.className}
      {...(props.onClick ? { onClick: props.onClick } : {})}
    >
      {props.children}
    </a>
  );
}

function NavList({
  sections,
  collapsed,
  renderLink,
  onNavigate,
}: {
  sections: readonly NavSection[];
  collapsed: boolean;
  renderLink: NonNullable<AppShellProps["renderLink"]>;
  onNavigate?: () => void;
}) {
  return (
    <div className="flex flex-col gap-5">
      {sections.map((section) => (
        <div key={section.id} className="flex flex-col gap-1">
          {section.label && !collapsed && (
            <p className="text-text-muted px-3 pb-1 text-xs font-semibold tracking-wide uppercase">
              {section.label}
            </p>
          )}
          <ul className="flex flex-col gap-0.5">
            {section.items.map((item) => (
              <li key={item.id}>
                {renderLink(item, {
                  className: cn(
                    "group text-text-muted flex h-10 items-center gap-3 rounded-md px-3 text-sm font-medium transition-colors duration-150",
                    "hover:bg-muted hover:text-text [&_svg]:size-5 [&_svg]:shrink-0",
                    item.active &&
                      "bg-accent-subtle text-accent-text hover:bg-accent-subtle hover:text-accent-text",
                    collapsed && "justify-center px-0",
                    focusRing,
                  ),
                  ...(onNavigate ? { onClick: onNavigate } : {}),
                  children: (
                    <>
                      {item.icon}
                      <span className={cn("flex-1 truncate", collapsed && "sr-only")}>
                        {item.label}
                      </span>
                      {item.badge != null && !collapsed && (
                        <span className="ml-auto">{item.badge}</span>
                      )}
                    </>
                  ),
                })}
              </li>
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}

/**
 * Admin layout: collapsible sidebar on desktop, drawer on mobile, sticky top bar, skip link and a single
 * <main> landmark.
 */
export function AppShell({
  brand,
  brandCollapsed,
  navigation,
  topBarStart,
  topBarEnd,
  collapsed: collapsedProp,
  defaultCollapsed = false,
  onCollapsedChange,
  renderLink = defaultLink,
  embedded = false,
  children,
  className,
}: AppShellProps) {
  const [collapsedState, setCollapsedState] = useState(defaultCollapsed);
  const collapsed = collapsedProp ?? collapsedState;
  const [mobileOpen, setMobileOpen] = useState(false);
  const sidebarId = useId();
  const mainId = `${sidebarId}-main`;

  const toggle = () => {
    const next = !collapsed;
    setCollapsedState(next);
    onCollapsedChange?.(next);
  };

  return (
    <div
      className={cn(
        "bg-background text-text flex",
        embedded ? "h-full min-h-0" : "min-h-dvh",
        className,
      )}
    >
      <a
        href={`#${mainId}`}
        className="bg-primary text-on-primary sr-only z-[1600] rounded-md px-4 py-2 focus:not-sr-only focus:fixed focus:top-2 focus:left-2"
      >
        Skip to content
      </a>

      <aside
        id={sidebarId}
        aria-label="Main navigation"
        className={cn(
          "border-border bg-surface hidden shrink-0 flex-col border-r transition-[width] duration-200 md:flex",
          embedded ? "h-full" : "sticky top-0 h-dvh",
          collapsed ? "w-16" : "w-64",
        )}
      >
        <div
          className={cn(
            "border-border flex h-16 items-center border-b px-4",
            collapsed && "justify-center px-0",
          )}
        >
          {collapsed ? (brandCollapsed ?? brand) : brand}
        </div>
        <nav aria-label="Main" className="flex-1 overflow-y-auto p-2">
          <NavList sections={navigation} collapsed={collapsed} renderLink={renderLink} />
        </nav>
        <div className={cn("border-border border-t p-2", collapsed && "flex justify-center")}>
          <IconButton
            label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
            aria-expanded={!collapsed}
            aria-controls={sidebarId}
            icon={
              collapsed ? (
                <PanelLeftOpen aria-hidden="true" />
              ) : (
                <PanelLeftClose aria-hidden="true" />
              )
            }
            onClick={toggle}
          />
        </div>
      </aside>

      <div className={cn("flex min-w-0 flex-1 flex-col", embedded && "overflow-y-auto")}>
        <header className="border-border bg-surface/95 supports-[backdrop-filter]:bg-surface/85 sticky top-0 z-[1100] flex h-16 items-center gap-3 border-b px-4 backdrop-blur">
          <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
            <IconButton
              className="md:hidden"
              label="Open navigation"
              aria-expanded={mobileOpen}
              icon={<Menu aria-hidden="true" />}
              onClick={() => setMobileOpen(true)}
            />
            <SheetContent side="left" title={brand} className="p-4">
              <nav aria-label="Main">
                <NavList
                  sections={navigation}
                  collapsed={false}
                  renderLink={renderLink}
                  onNavigate={() => setMobileOpen(false)}
                />
              </nav>
            </SheetContent>
          </Sheet>
          <div className="flex min-w-0 flex-1 items-center gap-3">{topBarStart}</div>
          {topBarEnd && <div className="flex items-center gap-2">{topBarEnd}</div>}
        </header>
        <main id={mainId} tabIndex={-1} className="flex-1 p-4 outline-none md:p-6">
          {children}
        </main>
      </div>
    </div>
  );
}
