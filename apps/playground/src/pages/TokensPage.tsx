import { type ColorRole, tokens } from "@fresh/design-tokens";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@fresh/ui";

const kebab = (s: string) => s.replace(/([a-z0-9])([A-Z])/g, "$1-$2").toLowerCase();

function Swatch({ hex, label }: { hex: string; label: string }) {
  return (
    <div className="flex items-center gap-2">
      <span
        aria-hidden="true"
        className="border-border size-8 shrink-0 rounded-md border"
        style={{ backgroundColor: hex }}
      />
      <span className="text-text-muted font-mono text-xs">
        {label} {hex}
      </span>
    </div>
  );
}

export function TokensPage() {
  const roles = Object.keys(tokens.color.light) as ColorRole[];
  return (
    <div className="grid gap-4">
      <Card>
        <CardHeader>
          <CardTitle>Brand</CardTitle>
          <CardDescription>The four brand colours, kept exactly.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {Object.entries(tokens.brand).map(([name, hex]) => (
            <div key={name} className="border-border overflow-hidden rounded-lg border">
              <div className="h-20" style={{ backgroundColor: hex }} />
              <div className="p-3">
                <p className="text-sm font-semibold">{name}</p>
                <p className="text-text-muted font-mono text-xs">{hex}</p>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Semantic colours</CardTitle>
          <CardDescription>
            Utility name, light and dark values. Use roles (bg-surface, text-text-muted), never raw
            hex.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <caption className="sr-only">Colour roles</caption>
              <thead>
                <tr className="border-border text-text-muted border-b text-left text-xs uppercase">
                  <th scope="col" className="py-2 pr-4">
                    Role
                  </th>
                  <th scope="col" className="py-2 pr-4">
                    Light
                  </th>
                  <th scope="col" className="py-2">
                    Dark
                  </th>
                </tr>
              </thead>
              <tbody>
                {roles.map((role) => (
                  <tr key={role} className="border-border border-b">
                    <th scope="row" className="py-2 pr-4 text-left font-mono text-xs font-normal">
                      {kebab(role)}
                    </th>
                    <td className="py-2 pr-4">
                      <Swatch hex={tokens.color.light[role]} label="" />
                    </td>
                    <td className="py-2">
                      <Swatch hex={tokens.color.dark[role]} label="" />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Type scale</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-2">
            {Object.entries(tokens.typeScale).map(([name, t]) => (
              <p
                key={name}
                style={{ fontSize: t.size, lineHeight: `${t.lineHeight}px` }}
                className="truncate"
              >
                <span className="text-text-muted mr-3 font-mono text-xs">
                  text-{name} {t.size}/{t.lineHeight}
                </span>
                Fresh seer fish
              </p>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Radii, shadows, spacing</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-6">
            <div className="flex flex-wrap gap-3">
              {Object.entries(tokens.radius)
                .filter(([name]) => name !== "full")
                .map(([name, px]) => (
                  <div key={name} className="flex flex-col items-center gap-1">
                    <div
                      className="border-accent bg-accent-subtle size-14 border-2"
                      style={{ borderRadius: px }}
                    />
                    <span className="text-text-muted font-mono text-xs">{name}</span>
                  </div>
                ))}
            </div>
            <div className="flex flex-wrap gap-4">
              {Object.keys(tokens.shadow).map((name) => (
                <div
                  key={name}
                  className="bg-surface flex size-20 items-center justify-center rounded-lg font-mono text-xs"
                  style={{ boxShadow: `var(--fo-shadow-${name})` }}
                >
                  {name}
                </div>
              ))}
            </div>
            <div className="flex flex-col gap-1">
              {Object.entries(tokens.spacing).map(([name, px]) => (
                <div key={name} className="flex items-center gap-2">
                  <span className="text-text-muted w-10 font-mono text-xs">{name}</span>
                  <span className="bg-accent h-2 rounded-sm" style={{ width: px }} />
                  <span className="text-text-muted font-mono text-xs">{px}px</span>
                </div>
              ))}
            </div>
            <p className="text-text-muted text-sm">
              Motion:{" "}
              {Object.entries(tokens.motion.duration)
                .map(([k, v]) => `${k} ${v}ms`)
                .join(" · ")}
            </p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
