// Our own placeholder art: one SVG per slot in a simple clay look (soft radial-gradient fills, no
// outlines), built from a few shared shapes so every placeholder matches the cast. No text inside images
// (D53). Regenerate with `pnpm --filter @fresh/illustrations placeholders`.
//
// A fill written as `fill="@#RRGGBB"` is a clay fill: the renderer swaps it for a radial gradient from a
// lighter to a darker shade of that colour, defined once per picture.
import { type Aspect, type Background, designSize } from "./manifest.ts";

const C = {
  ink: "#14302F",
  teal: "#0B7D70",
  tealDeep: "#075F55",
  coral: "#F07A5A",
  coralDeep: "#D8603F",
  mango: "#F5B544",
  cream: "#FFFDF8",
  iceBox: "#BFE3EE",
  silver: "#B9C7CC",
  silverDeep: "#8A99A0",
  steel: "#9AA8AD",
  wicker: "#D9A86C",
  wickerLine: "#B5814A",
  hair: "#2B1D17",
  grey: "#8E8E8E",
  roof: "#C8643E",
  band: "#DDE7E6",
  bindi: "#C2362B",
  glove: "#BFE3F5",
} as const;

/** Backdrop tints (the concept's sand-strong, teal-soft, ice, coral-soft and a soft mango). */
const TINT = {
  sand: "#F3E4CC",
  teal: "#D8EEEA",
  ice: "#DCEFF5",
  coral: "#FCE3DA",
  mango: "#FBE6BD",
} as const;
type Tint = keyof typeof TINT;

type Place = readonly [x: number, y: number, scale: number];
const at = ([x, y, s]: Place, body: string): string =>
  `<g transform="translate(${x} ${y})${s === 1 ? "" : ` scale(${s})`}">${body}</g>`;

// ---------- characters (bust, base at 0,0; head centre at 0,-150) ----------
const SKIN = { padma: "#C98A5E", ravi: "#A86E47", anil: "#B07A52", ammamma: "#D39A70" };

const torso = (fill: string) =>
  `<path d="M-72 0C-72-70-50-104 0-104S72-70 72 0Z" fill="@${fill}"/>`;
const head = (skin: string, smileY = -132) =>
  `<circle cy="-150" r="46" fill="@${skin}"/>` +
  `<g fill="${C.ink}"><circle cx="-16" cy="-151" r="5.5"/><circle cx="16" cy="-151" r="5.5"/></g>` +
  `<g fill="#fff"><circle cx="-14" cy="-153" r="1.8"/><circle cx="18" cy="-153" r="1.8"/></g>` +
  `<path d="M-14 ${smileY}q14 12 28 0" fill="none" stroke-width="4"/>` +
  `<g fill="${C.coral}" opacity=".35"><circle cx="-29" cy="-136" r="8"/><circle cx="29" cy="-136" r="8"/></g>`;
/** A raised arm (side -1 left, 1 right). */
const arm = (side: 1 | -1, sleeve: string, skin: string) =>
  `<path d="M${side * 54}-78Q${side * 86}-108 ${side * 92}-168" fill="none" stroke="${sleeve}" stroke-width="20"/><circle cx="${side * 94}" cy="-182" r="15" fill="@${skin}"/>`;
const earrings = `<g fill="${C.mango}"><circle cx="-46" cy="-136" r="4"/><circle cx="46" cy="-136" r="4"/></g>`;

export type Figure = "FISHMONGER" | "RIDER" | "SUPPORT_AGENT" | "HOME_COOK" | "POMFRET";

function figure(kind: Figure, arms: readonly (1 | -1)[] = []): string {
  switch (kind) {
    case "FISHMONGER": // Padma: coral headscarf over a low bun, white kurta, teal apron, light-blue gloves
      return (
        `<circle cx="-40" cy="-118" r="15" fill="@${C.hair}"/>` +
        torso(C.cream) +
        arms.map((s) => arm(s, C.cream, C.glove)).join("") +
        `<path d="M-36 0l4-76h64l4 76Z" fill="@${C.teal}"/><circle cy="-40" r="9" fill="${C.cream}" opacity=".85"/>` +
        head(SKIN.padma) +
        earrings +
        `<path d="M-53-144C-54-212 54-212 53-144C42-176-42-176-53-144Z" fill="@${C.coral}"/><circle cx="52" cy="-150" r="12" fill="@${C.coralDeep}"/>` +
        `<circle cy="-168" r="3.5" fill="${C.bindi}"/>`
      );
    case "RIDER": // Ravi: teal helmet with a clear visor, teal rain jacket with stripes, ice-blue cold backpack
      return (
        `<rect x="-104" y="-100" width="66" height="84" rx="16" fill="@${C.iceBox}"/><g transform="translate(-71 -58) scale(.7)">${snow}</g>` +
        torso(C.teal) +
        arms.map((s) => arm(s, C.teal, SKIN.ravi)).join("") +
        `<path d="M-70-34H70M-62-58H62" stroke="${C.band}" stroke-width="7"/><path d="M-40-100V0M40-100V0" stroke="${C.ink}" stroke-width="8" opacity=".75"/><path d="M-18-104L0-84 18-104Z" fill="${C.cream}"/>` +
        head(SKIN.ravi, -128) +
        `<path d="M-42-146C-40-98 40-98 42-146C34-120-34-120-42-146Z" fill="@${C.hair}"/><path d="M-12-124q12 9 24 0" fill="none" stroke="${C.cream}" stroke-width="4"/>` +
        `<path d="M-56-164C-56-230 56-230 56-164Z" fill="@${C.teal}"/><rect x="-58" y="-172" width="116" height="10" rx="5" fill="${C.tealDeep}"/>` +
        `<path d="M-46-164q46 44 92 0Z" fill="#fff" opacity=".22"/>`
      );
    case "SUPPORT_AGENT": // Anil: wavy hair, round thin glasses, teal polo, slim black headset
      return (
        torso(C.teal) +
        arms.map((s) => arm(s, C.teal, SKIN.anil)).join("") +
        `<path d="M-20-104L0-84 20-104Z" fill="${C.cream}"/>` +
        head(SKIN.anil) +
        `<g fill="none" stroke-width="3"><circle cx="-17" cy="-151" r="13"/><circle cx="17" cy="-151" r="13"/><path d="M-4-153h8"/></g>` +
        `<path d="M-46-156C-50-200-20-214 0-204C20-214 50-200 46-156C34-178 16-172 0-180C-16-172-34-178-46-156Z" fill="@${C.hair}"/>` +
        `<path d="M-52-150C-52-214 52-214 52-150" fill="none" stroke-width="6"/><rect x="-62" y="-166" width="16" height="30" rx="7" fill="@${C.ink}"/><rect x="46" y="-166" width="16" height="30" rx="7" fill="@${C.ink}"/>` +
        `<path d="M-54-136q8 28 34 26" fill="none" stroke-width="4"/><circle cx="-18" cy="-110" r="5" fill="${C.ink}"/>`
      );
    case "HOME_COOK": // Ammamma: mango saree, grey bun
      return (
        `<circle cy="-198" r="19" fill="@${C.grey}"/>` +
        torso(C.mango) +
        arms.map((s) => arm(s, C.mango, SKIN.ammamma)).join("") +
        `<path d="M-62-56Q-34-92 0-102" fill="none" stroke="${C.coral}" stroke-width="9"/>` +
        head(SKIN.ammamma) +
        earrings +
        `<path d="M-48-146C-50-198 50-198 48-146C38-172-38-172-48-146Z" fill="@${C.grey}"/>` +
        `<circle cy="-168" r="4" fill="${C.bindi}"/>`
      );
    case "POMFRET": // Chanduva: round silver pomfret, cheeky grin
      return (
        `<path d="M50 0L94-34L84 0L94 34Z" fill="@${C.silverDeep}"/><path d="M-20-44Q6-88 34-42Z" fill="@${C.silverDeep}"/><path d="M-16 44Q8 84 32 42Z" fill="@${C.silverDeep}"/>` +
        `<ellipse rx="64" ry="52" fill="@${C.silver}"/>` +
        `<circle cx="-26" cy="-12" r="14" fill="#fff"/><circle cx="-28" cy="-10" r="8" fill="${C.ink}"/><circle cx="-31" cy="-13" r="2.6" fill="#fff"/>` +
        `<circle cx="-8" cy="14" r="7" fill="${C.coral}" opacity=".4"/><path d="M-52 10q16 22 34 2Z" fill="#8A2E2E"/>`
      );
  }
}

// ---------- props ----------
const ice = (pts: readonly [number, number][]) =>
  `<g fill="#fff" opacity=".9">${pts.map(([x, y]) => `<rect x="${x - 6}" y="${y - 6}" width="12" height="12" rx="3"/>`).join("")}</g>`;
const snow = `<g stroke="#fff" stroke-width="4"><path d="M0-22v44M-19-11l38 22M-19 11l38-22"/></g>`;
const basket = `<path d="M-70-8H70L56 56H-56Z" fill="@${C.wicker}"/><path d="M-62 14H62M-58 36H58" stroke="${C.wickerLine}" stroke-width="4"/>`;
const box = (lid: string) =>
  `<rect x="-56" y="-48" width="112" height="22" rx="10" fill="@${lid}"/><rect x="-50" y="-30" width="100" height="70" rx="14" fill="@${C.iceBox}"/>`;

const MOTIFS = {
  basketFull: `<g fill="@${C.silver}"><ellipse cx="-28" cy="-16" rx="32" ry="13"/><ellipse cx="24" cy="-20" rx="32" ry="13"/></g>${basket}`,
  basketEmpty: `<path d="M-50-8C-50-74 50-74 50-8" fill="none" stroke="${C.wickerLine}" stroke-width="8"/>${basket}`,
  tray: `<rect x="-80" y="-12" width="160" height="34" rx="12" fill="@${C.steel}"/>${ice([
    [-40, 2],
    [-8, 6],
    [26, 1],
    [54, 6],
  ])}`,
  cuts: `<rect x="-90" y="0" width="180" height="26" rx="10" fill="@${C.steel}"/><ellipse cx="-50" cy="-12" rx="34" ry="13" fill="@${C.silver}"/><g fill="@${C.coral}"><rect x="-2" y="-26" width="22" height="22" rx="6"/><rect x="26" y="-26" width="22" height="22" rx="6"/><rect x="54" y="-26" width="22" height="22" rx="6"/></g>`,
  counter: `<rect x="-110" y="-12" width="220" height="24" rx="8" fill="@${C.wicker}"/>`,
  coldbox: `${box(C.teal)}<g transform="translate(0 6)">${snow}</g>`,
  coldboxOpen: `<rect x="-56" y="-78" width="112" height="22" rx="10" fill="@${C.teal}" transform="rotate(-22 -56 -40)"/><rect x="-50" y="-30" width="100" height="70" rx="14" fill="@${C.iceBox}"/>${ice(
    [
      [-28, -14],
      [6, -16],
      [30, -10],
    ],
  )}`,
  scooter: `<g transform="translate(-52 -40) scale(.55)">${box(C.teal)}</g><path d="M-84 30C-84 0-44-12 0-12H40L66-62H84L74 30Z" fill="@${C.coral}"/><g fill="${C.ink}"><circle cx="-56" cy="40" r="22"/><circle cx="56" cy="40" r="22"/></g><g fill="@${C.steel}"><circle cx="-56" cy="40" r="9"/><circle cx="56" cy="40" r="9"/></g>`,
  pin: `<path d="M0 70C-40 24-56 0-56-26a56 56 0 0 1 112 0C56 0 40 24 0 70Z" fill="@${C.teal}"/><circle cy="-26" r="20" fill="${C.cream}"/>`,
  barrier: `<path d="M-60 64V-10M60 64V-10" stroke="${C.steel}" stroke-width="9"/><rect x="-80" y="-30" width="160" height="32" rx="10" fill="@${C.coral}"/><path d="M-46-26l-14 24M-6-26l-14 24M34-26l-14 24M74-26l-14 24" stroke="#fff" stroke-width="10"/>`,
  heart: `<path d="M0 50C-60 10-70-20-50-40S-10-50 0-24C10-50 30-60 50-40S60 10 0 50Z" fill="@${C.coral}"/>`,
  bell: `<path d="M-6-62h12" stroke-width="10"/><path d="M-50 30C-44 20-40 0-40-20a40 40 0 0 1 80 0c0 20 4 40 10 50Z" fill="@${C.teal}"/><circle cy="42" r="12" fill="@${C.mango}"/>`,
  house: `<rect x="-60" y="-20" width="120" height="90" rx="8" fill="@${C.cream}"/><path d="M-78-14L0-80 78-14Z" fill="@${C.roof}"/><rect x="-18" y="18" width="36" height="52" rx="16" fill="@${C.teal}"/>`,
  mapCard: `<path d="M-40-30l26 8 28-8 26 8v60l-26-8-28 8-26-8Z" fill="@${C.cream}"/><path d="M-14-22v60M14-30v60" stroke="${C.steel}" stroke-width="3"/>`,
  magnifier: `<path d="M30 30l40 40" stroke-width="16"/><circle r="44" fill="${TINT.ice}" stroke="${C.teal}" stroke-width="12"/>`,
  phone: `<rect x="-34" y="-60" width="68" height="120" rx="14" fill="@${C.ink}"/><rect x="-26" y="-48" width="52" height="88" rx="8" fill="@${C.iceBox}"/>`,
  padlock: `<path d="M-14-6v-12a14 14 0 0 1 28 0v12" fill="none" stroke="${C.steel}" stroke-width="7"/><rect x="-22" y="-8" width="44" height="34" rx="8" fill="@${C.coral}"/>`,
  confetti: `<rect x="40" y="60" width="16" height="10" rx="3" fill="${C.coral}" transform="rotate(25 48 65)"/><rect x="330" y="80" width="16" height="10" rx="3" fill="${C.teal}" transform="rotate(-30 338 85)"/><rect x="70" y="180" width="14" height="9" rx="3" fill="${C.mango}" transform="rotate(60 77 184)"/><rect x="300" y="200" width="16" height="10" rx="3" fill="${C.coral}" transform="rotate(-50 308 205)"/><circle cx="110" cy="90" r="7" fill="${C.teal}"/><circle cx="260" cy="60" r="6" fill="${C.mango}"/><circle cx="350" cy="160" r="7" fill="${C.coral}"/>`,
  parcel: `<rect x="-44" y="-34" width="88" height="68" rx="10" fill="@${C.wicker}"/><path d="M0-34v68M-44 0h88" stroke="${C.teal}" stroke-width="8"/>`,
  parcelUntied: `<rect x="-44" y="-34" width="88" height="68" rx="10" fill="@${C.teal}"/><path d="M0-34q-30-30-44-10M0-34q26-36 40-16" fill="none" stroke="${C.coral}" stroke-width="6"/>`,
  speed: `<path d="M-60-20h50M-70 10h60M-56 40h40" stroke="${C.coral}" stroke-width="7" opacity=".6"/>`,
  clipboard: `<rect x="-44" y="-56" width="88" height="112" rx="12" fill="@${C.cream}"/><rect x="-20" y="-66" width="40" height="20" rx="6" fill="@${C.ink}"/><path d="M-22 4l16 16 30-34" fill="none" stroke="${C.teal}" stroke-width="10"/>`,
  net: `<path d="M-70-50Q0-80 70-50L50 50Q0 70-50 50Z" fill="${C.teal}" fill-opacity=".15" stroke="${C.teal}" stroke-width="5"/><path d="M-40-62L-30 58M0-66V64M40-62L30 58M-66-20H66M-58 20H58" fill="none" stroke="${C.teal}" stroke-width="3"/>`,
  cloud: `<path d="M-50 20a30 30 0 0 1 10-58a40 40 0 0 1 76 6a26 26 0 0 1 14 52Z" fill="@#D5DCDE"/><g fill="${C.teal}" opacity=".5"><path d="M-20 34q-8 14 0 18q8-4 0-18Z"/><path d="M12 40q-8 14 0 18q8-4 0-18Z"/></g>`,
  waves: `<path d="M-210 0q30-24 60 0t60 0 60 0 60 0 60 0 60 0 60 0v110h-420Z" fill="@#9FD3E2"/>`,
  boat: `<path d="M0 0v-50l34 40Z" fill="@${C.cream}"/><path d="M-50 0h100l-20 30h-60Z" fill="@${C.wicker}"/>`,
  spyglass: `<path d="M0 0l70-22" stroke="${C.mango}" stroke-width="16"/>`,
  scale: `<path d="M0-96v22" stroke="${C.steel}" stroke-width="6"/><circle cy="-30" r="44" fill="@${C.cream}" stroke="${C.steel}" stroke-width="8"/><path d="M0-30l22-22" stroke="${C.coral}" stroke-width="6"/><path d="M-26-56h0M26-56h0M-38-30h0M38-30h0M0-64h0" stroke-width="8"/><path d="M0 14v18" stroke="${C.steel}" stroke-width="5"/><ellipse cy="56" rx="34" ry="22" fill="@${C.silver}"/>`,
  sun: `<circle r="64" fill="@${C.coral}" opacity=".6"/>`,
  clock: `<circle r="56" fill="@${C.cream}" stroke="${C.teal}" stroke-width="12"/><path d="M0 0v-30M0 0l22 12" stroke-width="7"/>`,
  key: `<path d="M-6 0h66M44 0v18M58 0v12" stroke="${C.mango}" stroke-width="10"/><circle cx="-30" r="24" fill="@${C.mango}"/><circle cx="-30" r="8" fill="${TINT.sand}"/>`,
  shelves: `<path d="M220 70h160M220 130h160M220 190h160" stroke="${C.wickerLine}" stroke-width="6" opacity=".35"/>`,
  shelf: `<path d="M-110 40h220" stroke="${C.steel}" stroke-width="8"/>`,
  boxOpen: `<path d="M-50-20h100v60h-100Z" fill="@${C.wicker}"/><path d="M-50-20l-24-24h40l26 24M50-20l24-24h-40l-26 24" fill="@#E9C99A"/>`,
  teacup: `<path d="M30 0a14 14 0 0 1 0 22" fill="none" stroke="${C.steel}" stroke-width="6"/><path d="M-30-10h60v24a30 30 0 0 1-60 0Z" fill="@${C.cream}"/><path d="M-10-24q-8-12 0-24M10-24q-8-12 0-24" fill="none" stroke="${C.steel}" stroke-width="4"/>`,
  crate: `<rect x="-60" y="-30" width="120" height="60" rx="6" fill="@${C.wicker}"/><path d="M-60 0h120" stroke="${C.wickerLine}" stroke-width="4"/>`,
  door: `<path d="M-50 90V-40a50 50 0 0 1 100 0V90Z" fill="@${C.teal}"/><circle cx="30" cy="30" r="5" fill="${C.mango}"/>`,
  pot: `<path d="M-40-10h80q0 50-40 50t-40-50Z" fill="@${C.steel}"/><rect x="-46" y="-16" width="92" height="12" rx="6" fill="@${C.silverDeep}"/>`,
  zz: `<g fill="${C.teal}" opacity=".5"><circle r="5"/><circle cx="12" cy="-14" r="7"/></g>`,
} as const;

type Motif = keyof typeof MOTIFS;

type Part = { figure: Figure; at: Place; arms?: readonly (1 | -1)[] } | { motif: Motif; at: Place };

const f = (figure: Figure, x: number, y: number, s: number, arms?: (1 | -1)[]): Part =>
  arms ? { figure, at: [x, y, s], arms } : { figure, at: [x, y, s] };
const m = (motif: Motif, x: number, y: number, s = 1): Part => ({ motif, at: [x, y, s] });

interface Composition {
  tint: Tint;
  parts: readonly Part[];
}

/** What each slot's placeholder shows. Coordinates are in a 400-wide view box. */
export const COMPOSITIONS: Record<string, Composition> = {
  ONBOARDING_FRESH: {
    tint: "sand",
    parts: [
      f("FISHMONGER", 140, 300, 0.85),
      m("basketFull", 270, 200, 0.9),
      f("POMFRET", 285, 110, 0.4),
    ],
  },
  ONBOARDING_CLEANED: {
    tint: "teal",
    parts: [f("FISHMONGER", 120, 300, 0.85), m("counter", 290, 236), m("cuts", 290, 210, 0.75)],
  },
  ONBOARDING_DELIVERY: {
    tint: "coral",
    parts: [m("scooter", 220, 210, 1.15), f("RIDER", 210, 214, 0.62), f("POMFRET", 300, 90, 0.35)],
  },
  LOCATION_PICK: { tint: "teal", parts: [f("RIDER", 160, 360, 0.95), m("pin", 290, 200, 0.85)] },
  NOT_SERVICEABLE: {
    tint: "sand",
    parts: [f("RIDER", 150, 360, 0.95), m("barrier", 290, 280, 0.75), f("POMFRET", 300, 150, 0.38)],
  },
  EMPTY_CART: {
    tint: "sand",
    parts: [
      f("FISHMONGER", 150, 360, 0.95),
      f("POMFRET", 290, 240, 0.32),
      m("basketEmpty", 290, 280, 0.85),
    ],
  },
  EMPTY_WISHLIST: {
    tint: "coral",
    parts: [f("HOME_COOK", 160, 360, 0.95), m("heart", 295, 230, 0.8)],
  },
  EMPTY_ORDERS: {
    tint: "ice",
    parts: [f("RIDER", 145, 360, 0.95), m("coldboxOpen", 290, 290, 0.9)],
  },
  EMPTY_NOTIFICATIONS: {
    tint: "mango",
    parts: [m("bell", 270, 230, 1.05), f("POMFRET", 140, 200, 0.9), m("zz", 175, 130)],
  },
  EMPTY_ADDRESSES: {
    tint: "teal",
    parts: [m("house", 290, 230), f("HOME_COOK", 140, 360, 0.95), m("mapCard", 180, 300, 0.7)],
  },
  NO_SEARCH_RESULTS: {
    tint: "ice",
    parts: [
      f("POMFRET", 90, 150, 0.35),
      f("FISHMONGER", 150, 360, 0.95),
      m("tray", 270, 320, 0.65),
      m("magnifier", 275, 240, 0.85),
    ],
  },
  OUT_OF_STOCK: {
    tint: "sand",
    parts: [f("FISHMONGER", 145, 360, 0.95), m("tray", 275, 280, 0.8)],
  },
  LOGIN_OTP: {
    tint: "teal",
    parts: [f("HOME_COOK", 150, 360, 0.95), m("phone", 290, 260, 0.9), m("padlock", 290, 150, 0.9)],
  },
  ORDER_PLACED: {
    tint: "mango",
    parts: [
      m("confetti", 0, 0),
      f("FISHMONGER", 200, 360, 0.9, [-1, 1]),
      f("POMFRET", 320, 130, 0.42),
    ],
  },
  ORDER_PACKING: {
    tint: "ice",
    parts: [
      f("FISHMONGER", 140, 360, 0.95),
      m("coldboxOpen", 290, 300, 0.85),
      m("parcel", 290, 250, 0.5),
    ],
  },
  ORDER_ON_THE_WAY: {
    tint: "teal",
    parts: [m("speed", 90, 200), m("scooter", 230, 210, 1.15), f("RIDER", 220, 214, 0.62)],
  },
  ORDER_DELIVERED: {
    tint: "sand",
    parts: [f("RIDER", 110, 300, 0.85), m("parcel", 200, 210, 0.6), f("HOME_COOK", 290, 300, 0.85)],
  },
  ORDER_CANCELLED: {
    tint: "coral",
    parts: [f("POMFRET", 170, 200, 1), m("parcelUntied", 280, 270, 0.8)],
  },
  COMPLAINT_RECEIVED: {
    tint: "teal",
    parts: [f("SUPPORT_AGENT", 145, 360, 0.95), m("clipboard", 295, 230, 0.9)],
  },
  HELP_SUPPORT: { tint: "mango", parts: [f("SUPPORT_AGENT", 200, 380, 1.1, [1])] },
  ERROR_GENERIC: {
    tint: "coral",
    parts: [f("FISHMONGER", 145, 360, 0.95), f("POMFRET", 295, 230, 0.35), m("net", 290, 230, 0.9)],
  },
  OFFLINE: { tint: "ice", parts: [f("RIDER", 160, 360, 0.95, [1]), m("cloud", 300, 110, 0.75)] },
  NOT_FOUND: {
    tint: "ice",
    parts: [
      m("waves", 200, 300),
      m("boat", 300, 290, 0.6),
      f("POMFRET", 150, 180, 0.85),
      m("spyglass", 170, 170, 0.9),
    ],
  },
  HOME_HERO: {
    tint: "teal",
    parts: [
      m("boat", 330, 140, 0.4),
      m("boat", 370, 160, 0.3),
      f("FISHMONGER", 110, 300, 0.8),
      m("basketFull", 205, 210, 0.55),
      f("RIDER", 300, 300, 0.8),
      f("POMFRET", 205, 100, 0.35),
    ],
  },
  FEATURE_HONEST_WEIGHT: {
    tint: "sand",
    parts: [f("FISHMONGER", 130, 380, 0.9), m("scale", 290, 220)],
  },
  FEATURE_CLEANED_YOUR_WAY: {
    tint: "teal",
    parts: [f("FISHMONGER", 130, 380, 0.9), m("cuts", 280, 270, 0.95), f("POMFRET", 300, 140, 0.4)],
  },
  FEATURE_COLD_CHAIN: {
    tint: "ice",
    parts: [
      f("RIDER", 115, 380, 0.85),
      m("coldboxOpen", 270, 300, 1.1),
      f("POMFRET", 270, 250, 0.4),
    ],
  },
  FEATURE_SAME_DAY: {
    tint: "coral",
    parts: [m("sun", 310, 150), m("clock", 285, 210, 1.05), f("RIDER", 130, 380, 0.9, [-1])],
  },
  ADMIN_LOGIN: {
    tint: "teal",
    parts: [m("shelves", 0, 0), f("FISHMONGER", 140, 300, 0.85), m("key", 290, 180)],
  },
  ADMIN_EMPTY: {
    tint: "sand",
    parts: [m("shelf", 230, 300), m("boxOpen", 240, 262, 0.9), f("POMFRET", 120, 180, 0.6)],
  },
  ADMIN_ALL_CAUGHT_UP: {
    tint: "teal",
    parts: [
      m("crate", 275, 310, 0.8),
      f("POMFRET", 275, 258, 0.32),
      m("zz", 300, 222),
      f("FISHMONGER", 140, 360, 0.95),
      m("teacup", 225, 270, 0.7),
    ],
  },
  ADMIN_NO_ACCESS: {
    tint: "sand",
    parts: [
      m("door", 290, 250),
      m("padlock", 290, 240, 0.8),
      f("SUPPORT_AGENT", 135, 360, 0.95, [1]),
    ],
  },
};

const shade = (hex: string, amount: number): string => {
  const n = Number.parseInt(hex.slice(1), 16);
  const ch = (v: number) =>
    Math.max(0, Math.min(255, v + amount))
      .toString(16)
      .padStart(2, "0");
  return `#${ch(n >> 16)}${ch((n >> 8) & 255)}${ch(n & 255)}`;
};

/** Swaps every clay fill `@#RRGGBB` for a gradient, one definition per colour. */
function clay(body: string): { defs: string; body: string } {
  const ids = new Map<string, string>();
  const out = body.replace(/="@(#[0-9A-Fa-f]{6})"/g, (_m, hex: string) => {
    let id = ids.get(hex);
    if (!id) {
      id = `g${ids.size.toString(36)}`;
      ids.set(hex, id);
    }
    return `="url(#${id})"`;
  });
  const defs = [...ids]
    .map(
      ([hex, id]) =>
        `<radialGradient id="${id}" cx=".38" cy=".28" r=".8"><stop stop-color="${shade(hex, 38)}"/><stop offset="1" stop-color="${shade(hex, -28)}"/></radialGradient>`,
    )
    .join("");
  return { defs, body: out };
}

/** Renders one slot's placeholder SVG. Throws when the slot has no composition. */
export function renderPlaceholder(slot: string, aspect: Aspect, background: Background): string {
  const comp = COMPOSITIONS[slot];
  if (!comp)
    throw new Error(
      `No placeholder composition for slot ${slot}; add one to src/placeholder-art.ts.`,
    );
  const { width, height } = designSize(aspect);
  const h = aspect === "1:1" ? 400 : 300;
  const cy = h / 2;
  const tint = TINT[comp.tint];

  const shape =
    background !== "TRANSPARENT"
      ? `<rect width="400" height="${h}"/>`
      : aspect === "1:1"
        ? `<circle cx="200" cy="${cy}" r="172"/>`
        : `<ellipse cx="200" cy="${cy}" rx="196" ry="146"/>`;
  const backdrop =
    background !== "TRANSPARENT"
      ? `<rect width="400" height="${h}" fill="${TINT.sand}"/><circle cx="200" cy="${cy}" r="${h * 0.42}" fill="${comp.tint === "sand" ? "#FBF4E8" : tint}"/>`
      : shape.replace("/>", ` fill="${tint}"/>`);

  const parts = comp.parts
    .map((p) => ("figure" in p ? at(p.at, figure(p.figure, p.arms)) : at(p.at, MOTIFS[p.motif])))
    .join("");
  const { defs, body } = clay(parts);

  return (
    `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 400 ${h}" aria-hidden="true">` +
    `<defs><clipPath id="c">${shape}</clipPath>${defs}</defs>${backdrop}` +
    `<g clip-path="url(#c)" stroke="${C.ink}" stroke-width="0" stroke-linecap="round" stroke-linejoin="round">${body}</g>` +
    `</svg>\n`
  );
}
