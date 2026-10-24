// Our own flat placeholder art: one SVG per slot, built from a few shared shapes so every placeholder
// matches. No text inside images (D53). Regenerate with `pnpm --filter @fresh/illustrations placeholders`.
import { type Aspect, type Background, designSize } from "./manifest.ts";

const C = {
  ink: "#0A2630",
  teal: "#0E9384",
  tealLight: "#8FD3C8",
  coral: "#F2795A",
  coralLight: "#F9B8A3",
  sand: "#FBF3E6",
  sandStrong: "#F3E3C7",
  sandDeep: "#E2C595",
  apron: "#EAD6B3",
  white: "#FFFFFF",
  silver: "#CDD7DA",
  silverHair: "#DADFE1",
  ice: "#DDF1F4",
  wicker: "#C98F4E",
  gold: "#E9AD55",
  roof: "#C8643E",
  hair: "#241611",
  bindi: "#C2362B",
} as const;

type Place = readonly [x: number, y: number, scale: number];
const at = ([x, y, s]: Place, body: string): string =>
  `<g transform="translate(${x} ${y})${s === 1 ? "" : ` scale(${s})`}">${body}</g>`;

// ---------- characters (bust, base at 0,0; head centre at 0,-150) ----------
const torso = (fill: string) => `<path d="M-72 0C-72-70-50-104 0-104S72-70 72 0Z" fill="${fill}"/>`;
const head = (skin: string, smile = true) =>
  `<circle cy="-150" r="46" fill="${skin}"/><g stroke="none" fill="${C.ink}"><circle cx="-16" cy="-152" r="5"/><circle cx="16" cy="-152" r="5"/></g>` +
  (smile ? `<path d="M-14-132q14 12 28 0" fill="none" stroke-width="4"/>` : "") +
  `<g stroke="none" fill="${C.coral}" opacity=".45"><circle cx="-29" cy="-136" r="7"/><circle cx="29" cy="-136" r="7"/></g>`;
/** A raised arm (side -1 left, 1 right), drawn as an ink stroke under a coloured stroke. */
const arm = (side: 1 | -1, sleeve: string, skin: string) => {
  const d = `M${side * 54}-78Q${side * 86}-108 ${side * 92}-168`;
  return `<path d="${d}" fill="none" stroke-width="28"/><path d="${d}" fill="none" stroke="${sleeve}" stroke-width="18"/><circle cx="${side * 94}" cy="-182" r="15" fill="${skin}"/>`;
};

const SKIN = { fishmonger: "#B97A56", rider: "#8A5A3C", agent: "#A46B49", cook: "#C9946F" };

export type Figure = "FISHMONGER" | "RIDER" | "SUPPORT_AGENT" | "HOME_COOK" | "POMFRET";

function figure(kind: Figure, arms: readonly (1 | -1)[] = []): string {
  switch (kind) {
    case "FISHMONGER":
      return (
        `<circle cy="-196" r="19" fill="${C.hair}"/>` +
        torso(C.teal) +
        arms.map((s) => arm(s, C.teal, SKIN.fishmonger)).join("") +
        `<path d="M-38 0l4-74h68l4 74Z" fill="${C.apron}"/><rect x="-16" y="-46" width="32" height="22" rx="6" fill="${C.sandDeep}" stroke-width="3"/>` +
        `<path d="M-62-56Q-34-92 0-102" fill="none" stroke="${C.coral}" stroke-width="7"/>` +
        head(SKIN.fishmonger) +
        `<path d="M-47-150C-50-200 50-200 47-150C38-178-38-178-47-150Z" fill="${C.hair}"/>` +
        `<g fill="${C.white}" stroke-width="2"><circle cx="-17" cy="-207" r="5"/><circle cy="-215" r="5"/><circle cx="17" cy="-207" r="5"/></g>` +
        `<circle cy="-171" r="3.5" fill="${C.bindi}" stroke="none"/>`
      );
    case "RIDER":
      return (
        `<path d="M38-140c22 30 18 64 8 92" fill="none" stroke="${C.hair}" stroke-width="16"/>` +
        torso(C.teal) +
        arms.map((s) => arm(s, C.teal, SKIN.rider)).join("") +
        `<path d="M-70-34H70" stroke="${C.silverHair}" stroke-width="9"/><path d="M-18-104L0-80 18-104" fill="${C.sand}"/>` +
        head(SKIN.rider) +
        `<path d="M-54-148C-54-214 54-214 54-148Z" fill="${C.teal}"/><path d="M0-200V-152" stroke="${C.coral}" stroke-width="12" stroke-linecap="butt"/><path d="M-58-148H58" stroke-width="7"/>`
      );
    case "SUPPORT_AGENT":
      return (
        torso(C.coral) +
        arms.map((s) => arm(s, C.coral, SKIN.agent)).join("") +
        `<path d="M-20-104L0-82 20-104" fill="${C.white}"/>` +
        head(SKIN.agent, false) +
        `<path d="M-46-158C-46-206 46-206 46-158C32-182-32-182-46-158Z" fill="${C.hair}"/>` +
        `<path d="M-45-142C-44-96 44-96 45-142C34-116-34-116-45-142Z" fill="${C.hair}"/><path d="M-12-122q12 9 24 0" fill="none" stroke="${C.sand}" stroke-width="4"/>` +
        `<path d="M-52-152C-52-212 52-212 52-152" fill="none" stroke="${C.teal}" stroke-width="9"/><rect x="-62" y="-168" width="16" height="32" rx="7" fill="${C.teal}"/><rect x="46" y="-168" width="16" height="32" rx="7" fill="${C.teal}"/><path d="M-54-138q8 30 34 28" fill="none" stroke="${C.teal}" stroke-width="5"/>`
      );
    case "HOME_COOK":
      return (
        torso("#F4EBDD") +
        arms.map((s) => arm(s, "#F4EBDD", SKIN.cook)).join("") +
        `<g stroke="${C.teal}" stroke-width="3" opacity=".6"><path d="M-36-96V0M0-104V0M36-96V0"/></g><path d="M30-100l26 16-12 84H26Z" fill="${C.coral}"/>` +
        head(SKIN.cook, false) +
        `<path d="M-48-148C-56-176-34-192-16-190M48-148C56-176 34-192 16-190M-30-188Q0-206 30-188" fill="none" stroke="${C.silverHair}" stroke-width="14"/>` +
        `<path d="M-24-136C-12-147 12-147 24-136C12-127-12-127-24-136Z" fill="${C.silverHair}" stroke-width="3"/>`
      );
    case "POMFRET":
      return (
        `<path d="M50 0L92-32V32Z" fill="${C.tealLight}"/><path d="M-24-44Q4-86 30-42Z" fill="${C.tealLight}"/><path d="M-20 44Q4 84 28 42Z" fill="${C.tealLight}"/>` +
        `<ellipse rx="64" ry="52" fill="${C.silver}"/><path d="M-4-22q14 22 0 44" fill="none" stroke-width="3" opacity=".4"/>` +
        `<circle cx="-28" cy="-10" r="11" fill="${C.white}"/><circle cx="-30" cy="-10" r="6" fill="${C.ink}" stroke="none"/>` +
        `<circle cx="-18" cy="14" r="8" fill="${C.coral}" opacity=".55" stroke="none"/><path d="M-50 16q8 8 16 2" fill="none" stroke-width="4"/>`
      );
  }
}

// ---------- props ----------
const ice = (pts: readonly [number, number][]) =>
  `<g fill="${C.ice}" stroke="none">${pts.map(([x, y]) => `<circle cx="${x}" cy="${y}" r="6"/>`).join("")}</g>`;

const basketShape = `<path d="M-70-8H70L56 56H-56Z" fill="${C.wicker}"/><path d="M-62 14H62M-58 36H58" fill="none" stroke-width="3"/>`;

const MOTIFS = {
  basketFull: `<g fill="${C.silver}"><ellipse cx="-28" cy="-16" rx="32" ry="13"/><ellipse cx="24" cy="-20" rx="32" ry="13"/></g>${basketShape}${ice(
    [
      [-40, -2],
      [10, -4],
      [44, -2],
    ],
  )}`,
  basketEmpty: `<path d="M-50-8C-50-74 50-74 50-8" fill="none" stroke-width="8"/>${basketShape}`,
  tray: `<rect x="-80" y="-12" width="160" height="34" rx="12" fill="${C.silver}"/>${ice([
    [-40, 2],
    [-10, 6],
    [25, 1],
    [52, 6],
  ])}`,
  cuts: `<rect x="-90" y="0" width="180" height="26" rx="10" fill="${C.silver}"/><ellipse cx="-50" cy="-12" rx="34" ry="13" fill="${C.silver}"/><g fill="${C.coralLight}"><circle cx="8" cy="-12" r="14"/><circle cx="38" cy="-12" r="14"/><rect x="58" y="-22" width="18" height="18" rx="5"/><rect x="66" y="-40" width="14" height="14" rx="4"/></g>`,
  counter: `<rect x="-110" y="-12" width="220" height="24" rx="8" fill="${C.silver}"/>`,
  coldbox: `<rect x="-56" y="-48" width="112" height="22" rx="10" fill="${C.teal}"/><rect x="-50" y="-30" width="100" height="70" rx="14" fill="${C.white}"/>${ice(
    [
      [-20, 0],
      [10, 14],
      [28, -6],
    ],
  )}`,
  coldboxOpen: `<rect x="-56" y="-78" width="112" height="22" rx="10" fill="${C.teal}" transform="rotate(-22 -56 -40)"/><rect x="-50" y="-30" width="100" height="70" rx="14" fill="${C.white}"/><g fill="#7FB8E0"><rect x="-38" y="-24" width="30" height="16" rx="5"/><rect x="8" y="-24" width="30" height="16" rx="5"/></g>${ice(
    [
      [-20, 14],
      [14, 22],
    ],
  )}`,
  scooter: `<circle cx="-56" cy="40" r="22" fill="${C.ink}"/><circle cx="56" cy="40" r="22" fill="${C.ink}"/><path d="M-84 30C-84 0-44-10 0-10H40L70-62H88L74 30Z" fill="${C.teal}"/><g transform="translate(-52 -38) scale(.55)"><rect x="-56" y="-48" width="112" height="22" rx="10" fill="${C.teal}"/><rect x="-50" y="-30" width="100" height="70" rx="14" fill="${C.white}"/></g>`,
  pin: `<path d="M0 70C-40 24-56 0-56-26a56 56 0 0 1 112 0C56 0 40 24 0 70Z" fill="${C.teal}"/><circle cy="-26" r="20" fill="${C.white}"/>`,
  barrier: `<path d="M-60 64V-10M60 64V-10" stroke-width="8"/><rect x="-80" y="-30" width="160" height="32" rx="10" fill="${C.coral}"/><path d="M-46-26l-14 24M-6-26l-14 24M34-26l-14 24M74-26l-14 24" stroke="${C.white}" stroke-width="10" stroke-linecap="butt"/><rect x="-80" y="-30" width="160" height="32" rx="10" fill="none"/>`,
  heart: `<path d="M0 50C-60 10-70-20-50-40S-10-50 0-24C10-50 30-60 50-40S60 10 0 50Z" fill="${C.coral}"/>`,
  bell: `<path d="M-6-62h12" stroke-width="10"/><path d="M-50 30C-44 20-40 0-40-20a40 40 0 0 1 80 0c0 20 4 40 10 50Z" fill="${C.teal}"/><circle cy="42" r="12" fill="${C.gold}"/><g fill="${C.tealLight}" stroke="none"><circle cx="66" cy="-50" r="6"/><circle cx="84" cy="-70" r="9"/></g>`,
  house: `<rect x="-60" y="-20" width="120" height="90" rx="8" fill="${C.sand}"/><path d="M-78-14L0-80 78-14Z" fill="${C.roof}"/><rect x="-18" y="18" width="36" height="52" rx="16" fill="${C.teal}"/>`,
  mapCard: `<path d="M-40-30l26 8 28-8 26 8v60l-26-8-28 8-26-8Z" fill="${C.white}"/><path d="M-14-22v60M14-30v60" fill="none" stroke-width="3" opacity=".4"/>`,
  magnifier: `<path d="M30 30l40 40" stroke-width="16"/><circle r="44" fill="${C.ice}" stroke="${C.teal}" stroke-width="12"/>`,
  phone: `<rect x="-34" y="-60" width="68" height="120" rx="14" fill="${C.ink}"/><rect x="-26" y="-48" width="52" height="88" rx="8" fill="${C.tealLight}" stroke="none"/>`,
  padlock: `<path d="M-14-6v-12a14 14 0 0 1 28 0v12" fill="none" stroke-width="7"/><rect x="-22" y="-8" width="44" height="34" rx="8" fill="${C.coral}"/>`,
  confetti: `<g stroke="none"><rect x="40" y="60" width="16" height="10" rx="3" fill="${C.coral}" transform="rotate(25 48 65)"/><rect x="330" y="80" width="16" height="10" rx="3" fill="${C.teal}" transform="rotate(-30 338 85)"/><rect x="70" y="180" width="14" height="9" rx="3" fill="${C.gold}" transform="rotate(60 77 184)"/><rect x="300" y="200" width="16" height="10" rx="3" fill="${C.coral}" transform="rotate(-50 308 205)"/><circle cx="110" cy="90" r="7" fill="${C.teal}"/><circle cx="260" cy="60" r="6" fill="${C.gold}"/><circle cx="350" cy="160" r="7" fill="${C.coralLight}"/><circle cx="50" cy="250" r="6" fill="${C.tealLight}"/></g>`,
  parcel: `<rect x="-44" y="-34" width="88" height="68" rx="10" fill="${C.sandDeep}"/><path d="M0-34v68M-44 0h88" stroke="${C.teal}" stroke-width="8" stroke-linecap="butt"/>`,
  parcelUntied: `<rect x="-44" y="-34" width="88" height="68" rx="10" fill="${C.teal}"/><path d="M0-34q-30-30-44-10M0-34q26-36 40-16" fill="none" stroke="${C.coral}" stroke-width="6"/>`,
  speed: `<path d="M-60-20h50M-70 10h60M-56 40h40" stroke-width="6" opacity=".35"/>`,
  clipboard: `<rect x="-44" y="-56" width="88" height="112" rx="12" fill="${C.sand}"/><rect x="-20" y="-66" width="40" height="20" rx="6" fill="${C.ink}"/><path d="M-22 4l16 16 30-34" fill="none" stroke="${C.teal}" stroke-width="10"/>`,
  net: `<path d="M-70-50Q0-80 70-50L50 50Q0 70-50 50Z" fill="${C.tealLight}" fill-opacity=".25" stroke="${C.teal}" stroke-width="5"/><path d="M-40-62L-30 58M0-66V64M40-62L30 58M-66-20H66M-58 20H58" fill="none" stroke="${C.teal}" stroke-width="3"/>`,
  cloud: `<path d="M-50 20a30 30 0 0 1 10-58a40 40 0 0 1 76 6a26 26 0 0 1 14 52Z" fill="${C.silverHair}"/><g fill="${C.tealLight}" stroke="none"><path d="M-20 34q-8 14 0 18q8-4 0-18Z"/><path d="M12 40q-8 14 0 18q8-4 0-18Z"/></g>`,
  waves: `<path d="M-210 0q30-24 60 0t60 0 60 0 60 0 60 0 60 0 60 0v110h-420Z" fill="${C.tealLight}"/>`,
  boat: `<path d="M0 0v-50l34 40Z" fill="${C.white}"/><path d="M-50 0h100l-20 30h-60Z" fill="${C.wicker}"/>`,
  spyglass: `<path d="M0 0l70-22" stroke-width="22"/><path d="M0 0l70-22" stroke="${C.gold}" stroke-width="14"/>`,
  scale: `<path d="M0-96v22" stroke-width="6"/><circle cy="-30" r="44" fill="${C.white}" stroke-width="6"/><path d="M0-30l22-22" stroke="${C.coral}" stroke-width="6"/><path d="M-26-56h0M26-56h0M-38-30h0M38-30h0M0-64h0" stroke-width="8"/><path d="M0 14v18" stroke-width="5"/><ellipse cy="56" rx="22" ry="34" fill="${C.silver}" transform="rotate(90 0 56)"/>`,
  sun: `<circle r="64" fill="${C.coralLight}" stroke="none"/>`,
  clock: `<circle r="56" fill="${C.white}" stroke="${C.teal}" stroke-width="12"/><path d="M0 0v-30M0 0l22 12" stroke-width="7"/>`,
  key: `<path d="M-6 0h66M44 0v18M58 0v12" stroke-width="16"/><path d="M-6 0h66M44 0v18M58 0v12" stroke="${C.gold}" stroke-width="9"/><circle cx="-30" r="24" fill="${C.gold}"/><circle cx="-30" r="8" fill="${C.sand}"/>`,
  shelves: `<g stroke-width="6" opacity=".25"><path d="M220 70h160M220 130h160M220 190h160"/></g>`,
  shelf: `<path d="M-110 40h220" stroke-width="8"/>`,
  boxOpen: `<path d="M-50-20h100v60h-100Z" fill="${C.sandDeep}"/><path d="M-50-20l-24-24h40l26 24M50-20l24-24h-40l-26 24" fill="${C.apron}"/>`,
  teacup: `<path d="M30 0a14 14 0 0 1 0 22" fill="none" stroke-width="6"/><path d="M-30-10h60v24a30 30 0 0 1-60 0Z" fill="${C.white}"/><path d="M-10-24q-8-12 0-24M10-24q-8-12 0-24" fill="none" stroke-width="4" opacity=".4"/>`,
  crate: `<rect x="-60" y="-30" width="120" height="60" rx="6" fill="${C.wicker}"/><path d="M-60 0h120" stroke-width="4"/>`,
  door: `<path d="M-50 90V-40a50 50 0 0 1 100 0V90Z" fill="${C.teal}"/><circle cx="30" cy="30" r="5" fill="${C.gold}"/>`,
  zz: `<g fill="${C.tealLight}" stroke="none"><circle r="5"/><circle cx="12" cy="-14" r="7"/></g>`,
} as const;

type Motif = keyof typeof MOTIFS;

type Part = { figure: Figure; at: Place; arms?: readonly (1 | -1)[] } | { motif: Motif; at: Place };

const f = (figure: Figure, x: number, y: number, s: number, arms?: (1 | -1)[]): Part =>
  arms ? { figure, at: [x, y, s], arms } : { figure, at: [x, y, s] };
const m = (motif: Motif, x: number, y: number, s = 1): Part => ({ motif, at: [x, y, s] });

/** What each slot's placeholder shows. Coordinates are in a 400-wide view box. */
export const COMPOSITIONS: Record<string, readonly Part[]> = {
  ONBOARDING_FRESH: [
    f("FISHMONGER", 140, 300, 0.85),
    m("basketFull", 270, 200, 0.9),
    f("POMFRET", 285, 110, 0.4),
  ],
  ONBOARDING_CLEANED: [
    f("FISHMONGER", 120, 300, 0.85),
    m("counter", 290, 236),
    m("cuts", 290, 210, 0.75),
  ],
  ONBOARDING_DELIVERY: [
    m("scooter", 220, 210, 1.15),
    f("RIDER", 210, 214, 0.62),
    f("POMFRET", 300, 90, 0.35),
  ],
  LOCATION_PICK: [f("RIDER", 160, 360, 0.95), m("pin", 290, 200, 0.85)],
  NOT_SERVICEABLE: [
    f("RIDER", 150, 360, 0.95),
    m("barrier", 290, 280, 0.75),
    f("POMFRET", 300, 150, 0.38),
  ],
  EMPTY_CART: [
    f("FISHMONGER", 150, 360, 0.95),
    f("POMFRET", 290, 240, 0.32),
    m("basketEmpty", 290, 280, 0.85),
  ],
  EMPTY_WISHLIST: [f("HOME_COOK", 160, 360, 0.95), m("heart", 295, 230, 0.8)],
  EMPTY_ORDERS: [f("RIDER", 145, 360, 0.95), m("coldboxOpen", 290, 290, 0.9)],
  EMPTY_NOTIFICATIONS: [m("bell", 270, 230, 1.05), f("POMFRET", 140, 200, 0.9), m("zz", 175, 130)],
  EMPTY_ADDRESSES: [
    m("house", 290, 230),
    f("HOME_COOK", 140, 360, 0.95),
    m("mapCard", 180, 300, 0.7),
  ],
  NO_SEARCH_RESULTS: [
    f("POMFRET", 90, 150, 0.35),
    f("FISHMONGER", 150, 360, 0.95),
    m("tray", 270, 320, 0.65),
    m("magnifier", 275, 240, 0.85),
  ],
  OUT_OF_STOCK: [f("FISHMONGER", 145, 360, 0.95), m("tray", 275, 280, 0.8)],
  LOGIN_OTP: [
    f("HOME_COOK", 150, 360, 0.95),
    m("phone", 290, 260, 0.9),
    m("padlock", 290, 150, 0.9),
  ],
  ORDER_PLACED: [
    m("confetti", 0, 0),
    f("FISHMONGER", 200, 360, 0.9, [-1, 1]),
    f("POMFRET", 320, 130, 0.42),
  ],
  ORDER_PACKING: [
    f("FISHMONGER", 140, 360, 0.95),
    m("coldboxOpen", 290, 300, 0.85),
    m("parcel", 290, 250, 0.5),
  ],
  ORDER_ON_THE_WAY: [m("speed", 90, 200), m("scooter", 230, 210, 1.15), f("RIDER", 220, 214, 0.62)],
  ORDER_DELIVERED: [
    f("RIDER", 110, 300, 0.85),
    m("parcel", 200, 210, 0.6),
    f("HOME_COOK", 290, 300, 0.85),
  ],
  ORDER_CANCELLED: [f("POMFRET", 170, 200, 1), m("parcelUntied", 280, 270, 0.8)],
  COMPLAINT_RECEIVED: [f("SUPPORT_AGENT", 145, 360, 0.95), m("clipboard", 295, 230, 0.9)],
  HELP_SUPPORT: [f("SUPPORT_AGENT", 200, 380, 1.1, [1])],
  ERROR_GENERIC: [
    f("FISHMONGER", 145, 360, 0.95),
    f("POMFRET", 295, 230, 0.35),
    m("net", 290, 230, 0.9),
  ],
  OFFLINE: [f("RIDER", 160, 360, 0.95, [1]), m("cloud", 300, 110, 0.75)],
  NOT_FOUND: [
    m("waves", 200, 300),
    m("boat", 300, 290, 0.6),
    f("POMFRET", 150, 180, 0.85),
    m("spyglass", 170, 170, 0.9),
  ],
  HOME_HERO: [
    m("boat", 330, 140, 0.4),
    m("boat", 370, 160, 0.3),
    f("FISHMONGER", 110, 300, 0.8),
    m("basketFull", 205, 210, 0.55),
    f("RIDER", 300, 300, 0.8),
    f("POMFRET", 205, 100, 0.35),
  ],
  FEATURE_HONEST_WEIGHT: [f("FISHMONGER", 130, 380, 0.9), m("scale", 290, 220)],
  FEATURE_CLEANED_YOUR_WAY: [
    f("FISHMONGER", 130, 380, 0.9),
    m("cuts", 280, 270, 0.95),
    f("POMFRET", 300, 140, 0.4),
  ],
  FEATURE_COLD_CHAIN: [
    f("RIDER", 115, 380, 0.85),
    m("coldboxOpen", 270, 300, 1.1),
    f("POMFRET", 270, 250, 0.4),
  ],
  FEATURE_SAME_DAY: [
    m("sun", 310, 150),
    m("clock", 285, 210, 1.05),
    f("RIDER", 130, 380, 0.9, [-1]),
  ],
  ADMIN_LOGIN: [m("shelves", 0, 0), f("FISHMONGER", 140, 300, 0.85), m("key", 290, 180)],
  ADMIN_EMPTY: [m("shelf", 230, 300), m("boxOpen", 240, 262, 0.9), f("POMFRET", 120, 180, 0.6)],
  ADMIN_ALL_CAUGHT_UP: [
    m("crate", 275, 310, 0.8),
    f("POMFRET", 275, 258, 0.32),
    m("zz", 300, 222),
    f("FISHMONGER", 140, 360, 0.95),
    m("teacup", 225, 270, 0.7),
  ],
  ADMIN_NO_ACCESS: [
    m("door", 290, 250),
    m("padlock", 290, 240, 0.8),
    f("SUPPORT_AGENT", 135, 360, 0.95, [1]),
  ],
};

/** Renders one slot's placeholder SVG. Throws when the slot has no composition. */
export function renderPlaceholder(slot: string, aspect: Aspect, background: Background): string {
  const parts = COMPOSITIONS[slot];
  if (!parts)
    throw new Error(
      `No placeholder composition for slot ${slot}; add one to src/placeholder-art.ts.`,
    );
  const { width, height } = designSize(aspect);
  const h = aspect === "1:1" ? 400 : 300;
  const cx = 200;
  const cy = h / 2;

  const backdrop =
    background === "SAND"
      ? `<rect width="400" height="${h}" fill="${C.sandStrong}"/><circle cx="${cx}" cy="${cy}" r="${h * 0.42}" fill="${C.sand}"/>`
      : aspect === "1:1"
        ? `<circle cx="${cx}" cy="${cy}" r="172" fill="${C.sandStrong}"/>`
        : `<ellipse cx="${cx}" cy="${cy}" rx="196" ry="146" fill="${C.sandStrong}"/>`;
  const clip =
    background === "SAND"
      ? `<rect width="400" height="${h}"/>`
      : aspect === "1:1"
        ? `<circle cx="${cx}" cy="${cy}" r="172"/>`
        : `<ellipse cx="${cx}" cy="${cy}" rx="196" ry="146"/>`;

  const body = parts
    .map((p) => ("figure" in p ? at(p.at, figure(p.figure, p.arms)) : at(p.at, MOTIFS[p.motif])))
    .join("");

  return (
    `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 400 ${h}" aria-hidden="true">` +
    `<clipPath id="c">${clip}</clipPath>${backdrop}` +
    `<g clip-path="url(#c)" stroke="${C.ink}" stroke-width="5" stroke-linejoin="round" stroke-linecap="round">${body}</g>` +
    `</svg>\n`
  );
}
