import { Button, AppShell, type NavSection, Select } from "@fresh/ui";
import { Fish, LogOut, Moon, Sun } from "lucide-react";
import { useState } from "react";
import { Navigate, NavLink, Outlet, useLocation } from "react-router";
import { useLogout, useMe } from "../api/auth";
import { ConflictDialogHost } from "../lib/conflict";
import { LocationScopeProvider, useLocationScope } from "./location-scope";
import { activeEntryId, visibleNavigation } from "./nav";
import { useCan, useSession } from "./services";

const ALL = "__all";

function LocationSwitcher() {
  const scope = useLocationScope();
  if (scope.locations.length === 0 && !scope.allLocations) return null;
  const options = [
    ...(scope.allLocations ? [{ value: ALL, label: "All locations" }] : []),
    ...scope.locations.map((l) => ({
      value: l.id,
      label: l.code ? `${l.name} (${l.code})` : l.name,
    })),
  ];
  // A single location needs no switcher, only its name.
  if (options.length === 1) {
    return <span className="text-text-muted truncate text-sm">{options[0]?.label}</span>;
  }
  return (
    <div className="w-56 max-w-full">
      <Select
        aria-label="Fulfillment location"
        options={options}
        value={scope.locationId ?? ALL}
        onValueChange={(v) => scope.setLocationId(v === ALL ? null : v)}
      />
    </div>
  );
}

function ThemeToggle() {
  const [dark, setDark] = useState(() => document.documentElement.dataset.theme === "dark");
  return (
    <Button
      variant="ghost"
      size="icon"
      aria-label={dark ? "Use light theme" : "Use dark theme"}
      title={dark ? "Use light theme" : "Use dark theme"}
      onClick={() => {
        const next = !dark;
        setDark(next);
        document.documentElement.dataset.theme = next ? "dark" : "light";
        try {
          window.localStorage.setItem("fresh-orbit.admin.theme", next ? "dark" : "light");
        } catch {
          // Preference not remembered when storage is blocked.
        }
      }}
    >
      {dark ? <Sun aria-hidden="true" /> : <Moon aria-hidden="true" />}
    </Button>
  );
}

function UserMenu() {
  const { profile } = useSession();
  const logout = useLogout();
  return (
    <div className="flex items-center gap-2">
      {profile && (
        <span className="hidden text-right text-sm leading-tight lg:block">
          <span className="block font-medium">{profile.fullName}</span>
          <span className="text-text-muted block text-xs">
            {profile.roles.map((r) => r.name).join(", ")}
          </span>
        </span>
      )}
      <Button
        variant="ghost"
        size="sm"
        leadingIcon={<LogOut aria-hidden="true" />}
        loading={logout.isPending}
        onClick={() => logout.mutate()}
      >
        Sign out
      </Button>
    </div>
  );
}

function Shell() {
  const can = useCan();
  const { pathname } = useLocation();
  // Fresh names and roles for the user menu; permissions themselves come from the token.
  useMe();
  const active = activeEntryId(pathname);
  const navigation: NavSection[] = visibleNavigation(can).map((g) => ({
    id: g.id,
    ...(g.label ? { label: g.label } : {}),
    items: g.items.map((i) => ({
      id: i.id,
      label: i.label,
      href: i.href,
      icon: i.icon,
      active: i.id === active,
    })),
  }));

  return (
    <AppShell
      brand={
        <span className="flex items-center gap-2 font-semibold">
          <span className="bg-primary text-on-primary flex size-8 items-center justify-center rounded-md">
            <Fish aria-hidden="true" className="size-4" />
          </span>
          Fresh Orbit
        </span>
      }
      brandCollapsed={
        <span className="bg-primary text-on-primary flex size-8 items-center justify-center rounded-md">
          <Fish aria-hidden="true" className="size-4" />
          <span className="sr-only">Fresh Orbit</span>
        </span>
      }
      navigation={navigation}
      renderLink={(item, props) => (
        <NavLink
          to={item.href}
          end={item.href === "/"}
          className={props.className}
          {...(props.onClick ? { onClick: props.onClick } : {})}
        >
          {props.children}
        </NavLink>
      )}
      topBarStart={<LocationSwitcher />}
      topBarEnd={
        <>
          <ThemeToggle />
          <UserMenu />
        </>
      }
    >
      <Outlet />
      <ConflictDialogHost />
    </AppShell>
  );
}

/** Signed-in area. Signed-out or expired sessions go to /login and come back to the same page. */
export function AdminLayout() {
  const { status } = useSession();
  const location = useLocation();
  if (status !== "signed-in") {
    const next = encodeURIComponent(`${location.pathname}${location.search}`);
    const reason = status === "expired" ? "&reason=expired" : "";
    return <Navigate to={`/login?next=${next}${reason}`} replace />;
  }
  return (
    <LocationScopeProvider>
      <Shell />
    </LocationScopeProvider>
  );
}
