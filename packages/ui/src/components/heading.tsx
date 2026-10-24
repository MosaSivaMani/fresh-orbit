// Headings of the Bold blocks look (D88, D89): Baloo 2 800, an optional handwritten line in Dancing Script 700
// (tilted −3°, 1.12×), an optional mango brush swipe behind one line, and a small hand-drawn dash doodle at the
// corner. Every word is real text; the marks are CSS masks from @fresh/ui/styles.css.
import { type ComponentProps, type ReactNode, createElement } from "react";
import { cn } from "../lib/cn";

export type HeadingLevel = 1 | 2 | 3 | 4;

// ---------- Script ----------
export interface ScriptProps extends ComponentProps<"span"> {
  /** `inherit` keeps the surrounding colour (on colour blocks); `accent` is teal text (on the page). */
  tone?: "inherit" | "accent";
}

/** A handwritten word or line: Dancing Script 700, tilted −3°, 1.12× the surrounding size. */
export function Script({ tone = "inherit", className, ...props }: ScriptProps) {
  return (
    <span
      data-script=""
      className={cn(
        "font-script inline-block -rotate-3 text-[1.12em] leading-[1.05] font-bold tracking-normal",
        tone === "accent" && "text-accent-text",
        className,
      )}
      {...props}
    />
  );
}

// ---------- Brush ----------
export interface BrushProps extends ComponentProps<"span"> {
  /** `mango` (default) on paper, canvas, coral, teal and ice; `paper` on a mango block. */
  tone?: "mango" | "paper";
}

/** A painted brush swipe behind a word. Text on it stays ink, so it is readable in both themes. */
export function Brush({ tone = "mango", className, ...props }: BrushProps) {
  return (
    <span
      data-brush={tone}
      className={cn("fo-brush", tone === "paper" && "fo-brush-paper", className)}
      {...props}
    />
  );
}

// ---------- Heading ----------
const sizes = {
  /** Colour-block hero and empty states. */
  display: "text-[2.25rem] leading-[0.98] sm:text-5xl",
  /** Page titles. */
  page: "text-3xl leading-tight sm:text-4xl",
  /** Section titles. */
  section: "text-2xl leading-tight sm:text-3xl",
  /** Card and sheet titles. */
  card: "text-xl leading-tight",
} as const;

export type HeadingSize = keyof typeof sizes;

export interface HeadingProps extends Omit<ComponentProps<"h1">, "children"> {
  /** h1–h4. Default 2. */
  level?: HeadingLevel;
  /** Default `page` for h1, `section` otherwise. */
  size?: HeadingSize;
  /** First line, Baloo 2 800. */
  line1?: ReactNode;
  /** Optional second line. Handwritten unless `script` says otherwise. */
  line2?: ReactNode;
  /** Which line is handwritten. Default `line2` when there is a second line, otherwise none. */
  script?: "line1" | "line2" | "none";
  /** One line on a mango brush swipe (never both). */
  brush?: "line1" | "line2";
  /** Paper swipe, for headings on a mango block. */
  brushTone?: BrushProps["tone"];
  /** Colour of the handwritten line: `inherit` on colour blocks, `accent` (teal) on the page. Default inherit. */
  scriptTone?: ScriptProps["tone"];
  /** The small dash doodle at the top-right corner. Default false. */
  doodle?: boolean;
  /** Free content instead of lines, e.g. `What's cooking <Script>today?</Script>`. */
  children?: ReactNode;
}

/**
 * A heading in the display face. With `line1`/`line2` it stacks two lines, the handwritten one in Dancing Script;
 * with `children` it renders them inline (use `Script` and `Brush` for single words).
 */
export function Heading({
  level = 2,
  size,
  line1,
  line2,
  script,
  brush,
  brushTone,
  scriptTone = "inherit",
  doodle = false,
  className,
  children,
  ...props
}: HeadingProps) {
  return createElement(
    `h${level}`,
    {
      className: cn(
        "font-display font-extrabold tracking-[-0.01em] text-balance",
        sizes[size ?? (level === 1 ? "page" : "section")],
        doodle && "fo-doodle pr-[0.9em]",
        className,
      ),
      ...props,
    },
    children ?? headingLines({ line1, line2, script, brush, brushTone, scriptTone }),
  );
}

/** Renders `line1`/`line2` without the heading element, for components that own their heading tag. */
export function headingLines({
  line1,
  line2,
  script,
  brush,
  brushTone,
  scriptTone = "inherit",
}: Pick<
  HeadingProps,
  "line1" | "line2" | "script" | "brush" | "brushTone" | "scriptTone"
>): ReactNode {
  const which = script ?? (line2 != null ? "line2" : "none");
  return [line1, line2].map((content, i) => {
    const key = i === 0 ? "line1" : "line2";
    if (content == null) return null;
    let inner: ReactNode = content;
    if (which === key) inner = <Script tone={scriptTone}>{inner}</Script>;
    if (brush === key) inner = <Brush tone={brushTone}>{inner}</Brush>;
    return (
      <span key={key} className="block">
        {inner}
      </span>
    );
  });
}
