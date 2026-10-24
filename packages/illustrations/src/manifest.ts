// Types and pure rules for manifest.json, shared by the scripts and their tests. No file access here.
import { readImageSize } from "./image-size.ts";

export type Aspect = "1:1" | "4:3";
/** SAND: the sand backdrop. TRANSPARENT: cut-out subject. SCENE: a full picture with its own scenery. */
export type Background = "TRANSPARENT" | "SAND" | "SCENE";
export type Status = "PLACEHOLDER" | "FINAL";

export interface CharacterSheet {
  /** Short name used in docs and the playground (Padma, Ravi, Anil, Ammamma, Chanduva). */
  name: string;
  /** Who this is in the story; starts with the name. */
  role: string;
  /** Face, hair, clothes and props, in one paragraph. Copied into every prompt that uses the character. */
  description: string;
}

export interface SlotEntry {
  slot: string;
  /** The image the apps show: the placeholder SVG until the owner adds a final JPEG, PNG or WebP. */
  file: string;
  /** Our own placeholder, always kept (used whenever status is not FINAL). */
  placeholder: string;
  /** One plain sentence for screen readers. */
  alt: string;
  /** Lead character (a key of `characters`). */
  character: string;
  /** Other characters in the picture. */
  supporting?: string[];
  /** What happens in the picture, without style words. */
  scene: string;
  aspect: Aspect;
  background: Background;
  status: Status;
  /** Pixel size of the FINAL file (set by the import and optimize scripts; checked against the file). */
  width?: number;
  height?: number;
  /** Where the import script copied the final from (repo-relative), and its byte size then. */
  source?: string;
  sourceBytes?: number;
  /** Built by `pnpm --filter @fresh/illustrations prompts`; do not edit by hand. */
  prompt: string;
}

export interface Manifest {
  $description?: string;
  stylePrompt: string;
  promptSuffix: string;
  backgrounds: Record<Background, string>;
  aspects: Record<Aspect, string>;
  characters: Record<string, CharacterSheet>;
  slots: SlotEntry[];
}

/** The slots the product uses (next-steps §4, D53). Adding one means adding it here and in the manifest. */
export const REQUIRED_SLOTS = [
  "ONBOARDING_FRESH",
  "ONBOARDING_CLEANED",
  "ONBOARDING_DELIVERY",
  "LOCATION_PICK",
  "NOT_SERVICEABLE",
  "EMPTY_CART",
  "EMPTY_WISHLIST",
  "EMPTY_ORDERS",
  "EMPTY_NOTIFICATIONS",
  "EMPTY_ADDRESSES",
  "NO_SEARCH_RESULTS",
  "OUT_OF_STOCK",
  "LOGIN_OTP",
  "ORDER_PLACED",
  "ORDER_PACKING",
  "ORDER_ON_THE_WAY",
  "ORDER_DELIVERED",
  "ORDER_CANCELLED",
  "COMPLAINT_RECEIVED",
  "HELP_SUPPORT",
  "ERROR_GENERIC",
  "OFFLINE",
  "NOT_FOUND",
  "HOME_HERO",
  "FEATURE_HONEST_WEIGHT",
  "FEATURE_CLEANED_YOUR_WAY",
  "FEATURE_COLD_CHAIN",
  "FEATURE_SAME_DAY",
  "ADMIN_LOGIN",
  "ADMIN_EMPTY",
  "ADMIN_ALL_CAUGHT_UP",
  "ADMIN_NO_ACCESS",
] as const;

export const LIMITS = {
  finalMaxBytes: 600 * 1024,
  finalMinLongestSide: 1024,
  placeholderMaxBytes: 6 * 1024,
  /** Allowed difference between the image's ratio and its declared aspect. */
  aspectTolerance: 0.02,
} as const;

/** File types accepted as FINAL, with the format their header must show. */
export const FINAL_FORMATS = { jpg: "JPEG", png: "PNG", webp: "WEBP" } as const;
export type FinalExtension = keyof typeof FINAL_FORMATS;
export const finalExtension = (path: string): FinalExtension | null =>
  (/\.(jpg|png|webp)$/.exec(path)?.[1] as FinalExtension | undefined) ?? null;

/** File name stem for a slot: the slot code in lower snake case, e.g. EMPTY_CART -> empty_cart. */
export const fileStem = (slot: string): string => slot.toLowerCase();

export const PROMPT_SUFFIX =
  "No text, no letters, no numbers, no logos, no watermark. High resolution.";

const ASPECT_RATIO: Record<Aspect, number> = { "1:1": 1, "4:3": 4 / 3 };

/** Intrinsic pixel size we design for (placeholders use it; finals must match the ratio). */
export function designSize(aspect: Aspect): { width: number; height: number } {
  return aspect === "1:1" ? { width: 1024, height: 1024 } : { width: 1024, height: 768 };
}

const sentence = (s: string): string => s.trim().replace(/[.\s]+$/, "");

/** Builds a slot's prompt from its scene, the character sheet and the shared style block. */
export function composePrompt(manifest: Manifest, entry: SlotEntry): string {
  const cast = [entry.character, ...(entry.supporting ?? [])].map((key) => {
    const sheet = manifest.characters[key];
    if (!sheet) throw new Error(`Slot ${entry.slot}: character "${key}" is not in the sheet.`);
    return `${sheet.role}: ${sentence(sheet.description)}`;
  });
  return [
    sentence(entry.scene),
    ...cast.map(sentence),
    sentence(manifest.stylePrompt),
    sentence(manifest.backgrounds[entry.background]),
    sentence(manifest.aspects[entry.aspect]),
    manifest.promptSuffix,
  ].join(". ");
}

const SLOT_PATTERN = /^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$/;

/** Files the checks need to look at, read by the caller (so this module stays pure and testable). */
export interface FileFacts {
  /** File size in bytes, or null when the file does not exist. */
  size(path: string): number | null;
  /** The file bytes the header readers need (the whole file is fine; JPEG may need more than 64). */
  head(path: string): Uint8Array;
}

/** Every rule the `validate` script enforces. Returns readable problems; empty means valid. */
export function checkManifest(manifest: Manifest, files: FileFacts): string[] {
  const problems: string[] = [];
  const add = (slot: string, message: string) => problems.push(`${slot}: ${message}`);

  if (!manifest.stylePrompt?.trim()) problems.push("manifest: stylePrompt is missing.");
  for (const [key, sheet] of Object.entries(manifest.characters ?? {}))
    if (!sheet.name?.trim() || !sheet.role?.trim() || !sheet.description?.trim())
      problems.push(`manifest: character ${key} needs a name, a role and a description.`);
  if (manifest.promptSuffix !== PROMPT_SUFFIX)
    problems.push(`manifest: promptSuffix must be "${PROMPT_SUFFIX}".`);
  for (const word of ["pixar", "disney"]) {
    if (JSON.stringify(manifest).toLowerCase().includes(word))
      problems.push(`manifest: do not name other studios ("${word}"); describe the style instead.`);
  }

  const seenSlots = new Set<string>();
  const seenFiles = new Map<string, string>();
  for (const entry of manifest.slots) {
    const slot = entry.slot ?? "(no slot)";
    if (!SLOT_PATTERN.test(slot)) add(slot, "slot must be UPPER_SNAKE.");
    if (seenSlots.has(slot)) add(slot, "slot is listed twice.");
    seenSlots.add(slot);

    for (const [path, kind] of [
      [entry.file, "file"],
      [entry.placeholder, "placeholder"],
    ] as const) {
      if (!path) {
        add(slot, `${kind} is missing.`);
        continue;
      }
      const owner = seenFiles.get(path);
      if (owner && owner !== slot) add(slot, `${kind} ${path} is also used by ${owner}.`);
      seenFiles.set(path, slot);
      if (files.size(path) === null) add(slot, `${kind} ${path} does not exist.`);
    }

    if (!entry.alt?.trim()) add(slot, "alt is missing.");
    else if (!/^[A-Z].*\.$/.test(entry.alt.trim()) || entry.alt.split(/[.!?]\s/).length > 1)
      add(
        slot,
        "alt must be one plain sentence that starts with a capital and ends with a full stop.",
      );
    if (!manifest.characters[entry.character])
      add(slot, `character "${entry.character}" is unknown.`);
    for (const key of entry.supporting ?? [])
      if (!manifest.characters[key]) add(slot, `supporting character "${key}" is unknown.`);
    if (!entry.scene?.trim()) add(slot, "scene is missing.");
    if (!(entry.aspect in ASPECT_RATIO)) add(slot, "aspect must be 1:1 or 4:3.");
    if (!(entry.background in (manifest.backgrounds ?? {})))
      add(slot, "background must be SAND, TRANSPARENT or SCENE.");
    if (entry.status !== "PLACEHOLDER" && entry.status !== "FINAL")
      add(slot, "status must be PLACEHOLDER or FINAL.");

    if (!entry.prompt?.trim()) add(slot, "prompt is missing.");
    else if (!entry.prompt.endsWith(PROMPT_SUFFIX))
      add(slot, `prompt must end "${PROMPT_SUFFIX}".`);
    else {
      try {
        if (entry.prompt !== composePrompt(manifest, entry))
          add(slot, "prompt is out of date; run `pnpm --filter @fresh/illustrations prompts`.");
      } catch (error) {
        add(slot, (error as Error).message);
      }
    }

    if (entry.placeholder) {
      if (entry.placeholder !== `assets/${fileStem(slot)}.svg`)
        add(slot, `placeholder must be assets/${fileStem(slot)}.svg.`);
      const size = files.size(entry.placeholder);
      if (size !== null && size > LIMITS.placeholderMaxBytes)
        add(slot, `placeholder is ${size} bytes; keep it under ${LIMITS.placeholderMaxBytes}.`);
    }

    if (entry.status === "FINAL" && entry.file) {
      const ext = finalExtension(entry.file);
      if (!ext) add(slot, "a FINAL file must be a .jpg, .png or .webp image.");
      else if (entry.file !== `assets/${fileStem(slot)}.${ext}`)
        add(slot, `a FINAL file must be named assets/${fileStem(slot)}.${ext}.`);
      const size = files.size(entry.file);
      if (size !== null) {
        if (size > LIMITS.finalMaxBytes)
          add(
            slot,
            `file is ${Math.round(size / 1024)} KB; keep it at or under ${LIMITS.finalMaxBytes / 1024} KB (run the optimize script).`,
          );
        const dims = readImageSize(files.head(entry.file));
        if (!dims || (ext && dims.format !== FINAL_FORMATS[ext]))
          add(slot, "file is not a readable JPEG, PNG or WebP image matching its extension.");
        else {
          if (entry.width !== dims.width || entry.height !== dims.height)
            add(
              slot,
              `width and height must be the file size ${dims.width}x${dims.height}; run the import script.`,
            );
          if (entry.background === "TRANSPARENT" && !dims.alpha)
            add(slot, "background is TRANSPARENT but the file has no transparency.");
          const longest = Math.max(dims.width, dims.height);
          if (longest < LIMITS.finalMinLongestSide)
            add(
              slot,
              `file is ${dims.width}x${dims.height}; the longest side must be at least 1024 px.`,
            );
          const expected = ASPECT_RATIO[entry.aspect];
          if (
            expected &&
            Math.abs(dims.width / dims.height - expected) / expected > LIMITS.aspectTolerance
          )
            add(
              slot,
              `file is ${dims.width}x${dims.height}, which does not match aspect ${entry.aspect}; run the optimize script to crop it.`,
            );
        }
      }
    }
    if (entry.status === "PLACEHOLDER" && entry.file && entry.file !== entry.placeholder)
      add(
        slot,
        "status is PLACEHOLDER but file is not the placeholder; set status FINAL or point file back.",
      );
  }

  for (const slot of REQUIRED_SLOTS)
    if (!seenSlots.has(slot)) add(slot, "required slot is missing.");
  for (const slot of seenSlots)
    if (!(REQUIRED_SLOTS as readonly string[]).includes(slot))
      add(slot, "slot is not in REQUIRED_SLOTS (src/manifest.ts); add it there first.");

  return problems;
}

/** The fields the apps need at runtime (no prompts). */
export interface RuntimeEntry {
  slot: string;
  /** File name to load, relative to the folder the assets are served from. */
  src: string;
  alt: string;
  aspect: Aspect;
  background: Background;
  status: Status;
  width: number;
  height: number;
}

export const baseName = (path: string): string => path.slice(path.lastIndexOf("/") + 1);

export function toRuntime(entry: SlotEntry): RuntimeEntry {
  const final = entry.status === "FINAL";
  const size =
    final && entry.width && entry.height
      ? { width: entry.width, height: entry.height }
      : designSize(entry.aspect);
  return {
    slot: entry.slot,
    src: baseName(final ? entry.file : entry.placeholder),
    alt: entry.alt,
    aspect: entry.aspect,
    background: entry.background,
    status: entry.status,
    ...size,
  };
}
