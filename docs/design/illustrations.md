# Illustrations (D53)

Every picture in the website, the admin dashboard and the mobile app fills one **slot**. The slots, their alt text,
scenes and generation prompts live in `packages/illustrations/manifest.json` (`@fresh/illustrations`). Until the
owner's final image for a slot is imported, the apps show our own clay-look placeholder SVG for it.

Owner how-to (generate, import, optimize, validate): `packages/illustrations/README.md`.

## 1. Cast

The approved reference is [`characters/character-sheet.jpg`](characters/character-sheet.jpg) (see
[`characters/README.md`](characters/README.md)). Every image must match it: same faces, hair, clothes and colours.

![Approved character sheet](characters/character-sheet.jpg)

| Key in manifest | Name | Role | Look |
|---|---|---|---|
| `FISHMONGER` | Padma | Fishmonger, our lead | Coral headscarf over a low bun, white kurta, glossy sea-teal apron, light-blue gloves, dark-green rubber boots, often holds a silver fish |
| `RIDER` | Ravi | Delivery rider | Sea-teal helmet with clear visor, teal rain jacket with reflective stripes, ice-blue insulated backpack with a white snowflake, dark cargo trousers, white sneakers, short beard |
| `SUPPORT_AGENT` | Anil | Support agent | Round thin glasses, wavy black hair, teal polo, slim black headset, navy trousers |
| `HOME_COOK` | Ammamma | Home cook | Silver bun, red bindi, mango-yellow saree with coral border, gold bangles, steel pot and ladle |
| `POMFRET` | Chanduva | Sidekick | Round silver pomfret, big glossy eyes, rosy cheeks, open happy smile, fins as hands; a character, never food |

The full descriptions copied into every prompt are the `characters` entries in the manifest.

## 2. Style rules

- **Style block** (the manifest's `stylePrompt`, in every prompt): highly detailed, realistic 3D animated-film style
  character render, soft subsurface skin, natural hair and fabric, expressive friendly faces, warm cinematic studio
  light, shallow depth of field, brand accents sea teal `#0B7D70` and coral `#F07A5A`, coastal Andhra Pradesh.
- **Every prompt ends** "No text, no letters, no numbers, no logos, no watermark. High resolution." Labels such as
  "Fresh today" are real text in the UI (`Sticker`), never inside an image.
- **Never name another studio or brand** in prompts or docs; never copy characters, colours, names or layouts from
  other apps (references are ideas only). `validate` refuses the two studio names we were warned about.
- **Backgrounds** (`background` in the manifest):

  | Value | Meaning | How components show it |
  |---|---|---|
  | `SCENE` | A full picture with its own scenery (harbour at sunrise, market, kitchen); usually JPEG | Cropped with `object-fit: cover` to the slot's shape inside a rounded frame (24–32 px) |
  | `SAND` | Subject on the warm sand backdrop | Shown whole (`contain`) on a sand or tinted panel |
  | `TRANSPARENT` | Cut-out subject; PNG or WebP with alpha | Shown whole (`contain`), may float gently in heroes |

  `import` sets `SCENE` or `TRANSPARENT` from the file itself (alpha or not) and rebuilds the prompt to match.
- **Shapes**: `1:1` (empty states, features, status, onboarding tiles), `4:3` (onboarding, hero, delivery, admin
  login), `2:1` (onboarding food banners, 1024 × 512 design size), `3:2` (the home hero, 1024 × 683 design
  size) or `20:7` (the website's full-width hero, generated at 2400 × 840, 1024 × 358 design size), 2% tolerance.
- **Object-only slots**: the onboarding tiles and banners have no `character` (and no `supporting`); their prompt is
  the scene, the style block, the background and the shape only. Their scenes say "no people, no characters and no
  faces" because the style block is written for character renders.
- **Files**: `assets/<slot_lower_snake>.jpg|png|webp`, at most 600 KB, longest side at least 1024 px. Placeholders:
  `assets/<slot_lower_snake>.svg`, under 6 KB, no text, generated from `src/placeholder-art.ts`.
- **Alt text**: one plain sentence describing the picture. Check it after importing a final; components make the
  picture decorative (`alt=""`) when nearby text already says the same thing.

## 3. Slots

54 slots (32 from the handoff, 12 onboarding tiles and banners from the owner's onboarding reference, and later
additions below; the list in `REQUIRED_SLOTS`, `src/manifest.ts`, is the source of truth). Status as of this change:
17 FINAL (`LOGIN_OTP` and `SIGN_IN_COAST` since D285–D286, `ORDER_PLACED` since D291, four `CATEGORY_*` since D294),
37 PLACEHOLDER.
The playground's **Illustrated** tab shows every slot with its alt text and status.

| Slot | Shape | Cast | Website | Admin | Mobile |
|---|---|---|---|---|---|
| `ONBOARDING_FRESH` | 4:3 | Padma, Chanduva | Hero alternative, "why us" section | — | Onboarding page 1 |
| `ONBOARDING_CLEANED` | 4:3 | Padma | "Cleaned your way" section | — | Onboarding page 2 |
| `ONBOARDING_DELIVERY` | 4:3 | Ravi, Chanduva | Delivery promise section | — | Onboarding page 3 |
| `LOCATION_PICK` | 1:1 | Ravi | Pincode / location sheet | — | Location permission and pick |
| `NOT_SERVICEABLE` | 1:1 | Ravi, Chanduva | "We don't deliver here yet" + Notify me | — | Same |
| `EMPTY_CART` | 1:1 | Padma, Chanduva | Empty cart | — | Empty cart tab |
| `EMPTY_WISHLIST` | 1:1 | Ammamma | Empty wishlist | — | Same |
| `EMPTY_ORDERS` | 1:1 | Ravi | Account → Orders, none yet | — | Orders tab, none yet |
| `EMPTY_NOTIFICATIONS` | 1:1 | Chanduva | Notifications, none | — | Same |
| `EMPTY_ADDRESSES` | 1:1 | Ammamma | Account → Addresses, none | — | Same |
| `NO_SEARCH_RESULTS` | 1:1 | Padma, Chanduva | Search / filters with no result | — | Same |
| `OUT_OF_STOCK` | 1:1 | Padma | Product sold out / Notify me | — | Same |
| `LOGIN_OTP` | 1:1 | Ammamma | Login and OTP step | — | Login and OTP |
| `ORDER_PLACED` | 1:1 | Padma (waist-up, thumbs-up, seer fish; FINAL, D291) | Order success page | — | Order-placed header (D288), tracking card, orders list |
| `ORDER_PACKING` | 1:1 | Padma | Tracking: preparing / packed | — | Same |
| `ORDER_ON_THE_WAY` | 4:3 | Ravi | Tracking: out for delivery | — | Same |
| `ORDER_DELIVERED` | 4:3 | Ravi, Ammamma | Tracking: delivered | — | Same |
| `ORDER_CANCELLED` | 1:1 | Chanduva | Tracking: cancelled | — | Same |
| `COMPLAINT_RECEIVED` | 1:1 | Anil | Complaint submitted | — | Same |
| `HELP_SUPPORT` | 1:1 | Anil | Help page (`HelpHero`) | — | Help screen |
| `ERROR_GENERIC` | 1:1 | Padma, Chanduva | Error page / failed load | Failed load (size `sm`) | Error screen |
| `OFFLINE` | 1:1 | Ravi | Offline notice | — | No connection |
| `NOT_FOUND` | 1:1 | Chanduva | 404 page | 404 page | Unknown deep link |
| `HOME_HERO` | 4:3 | Padma, Ravi, Chanduva | Home hero (`HeroBanner`) | — | Home banner |
| `FEATURE_HONEST_WEIGHT` | 1:1 | Padma | Home feature card | — | Home feature card |
| `FEATURE_CLEANED_YOUR_WAY` | 1:1 | Padma, Chanduva | Home feature card | — | Same |
| `FEATURE_COLD_CHAIN` | 1:1 | Chanduva, Ravi | Home feature card | — | Same |
| `FEATURE_SAME_DAY` | 1:1 | Ravi | Home feature card | — | Same |
| `ADMIN_LOGIN` | 4:3 | Padma | — | Staff login side panel | — |
| `ADMIN_EMPTY` | 1:1 | Chanduva | — | Empty lists (size `sm`) | — |
| `ADMIN_ALL_CAUGHT_UP` | 1:1 | Padma, Chanduva | — | Nothing left to pack / no exceptions | — |
| `ADMIN_NO_ACCESS` | 1:1 | Anil | — | 403 page | — |
| `ONBOARDING_TILE_MORNING_CATCH` | 1:1 | none (a seer fish) | — | — | Onboarding page 1 tile "Morning catch" |
| `ONBOARDING_TILE_KEPT_CHILLED` | 1:1 | none (ice cubes) | — | — | Page 1 tile "Kept chilled" |
| `ONBOARDING_TILE_DELIVERED_FRESH` | 1:1 | none (delivery van) | — | — | Page 1 tile "Delivered fresh" |
| `ONBOARDING_TILE_YOUR_CUT` | 1:1 | none (steak, curry cut, fillet on a board) | — | — | Page 2 tile "Your cut" |
| `ONBOARDING_TILE_CLEANED_BY_HAND` | 1:1 | none (gloved hand, fish, bowl of water) | — | — | Page 2 tile "Cleaned by hand" |
| `ONBOARDING_TILE_NET_WEIGHT` | 1:1 | none (kitchen scale with fish pieces) | — | — | Page 2 tile "Pay net weight" |
| `ONBOARDING_TILE_PICK_SLOT` | 1:1 | none (alarm clock, calendar page) | — | — | Page 3 tile "Pick a slot" |
| `ONBOARDING_TILE_COLD_BOX` | 1:1 | none (open cold box with ice, fish tail, prawns) | — | — | Page 3 tile "Cold box" |
| `ONBOARDING_TILE_CASH_UPI` | 1:1 | none (rupee notes, phone with a tick) | — | — | Page 3 tile "Cash or UPI" |
| `ONBOARDING_BANNER_FRESH` | 2:1 | none (prawns, seer fish, crab, lemon, herbs on ice) | — | — | Page 1 food banner |
| `ONBOARDING_BANNER_CLEANED` | 2:1 | none (steaks, curry cut, fillet, knife, curry leaves, chillies) | — | — | Page 2 food banner |
| `ONBOARDING_BANNER_DELIVERY` | 2:1 | none (cold box, scooter, Indian doorstep) | — | — | Page 3 food banner |
| `HOME_HERO_CATCH` | 3:2 | `FISHMONGER` (Padma with the morning catch) | — | — | Home hero "Today's catch is in!", bottom-right |
| `HOME_CARD_CLEANED` | 1:1 | none (cleaned cuts on a board) | — | — | Home card "Cleaned your way" |
| `HOME_CARD_RIDER` | 1:1 | `RIDER` (Ravi with the cold box) | — | — | Home card "Kept cold till your door"; order-placed delivery-code note (D288) |
| `CATEGORY_FILLETS_STEAKS` | 1:1 | none (seer steaks, salmon fillet pieces, lemon on a banana leaf; FINAL, D294) | — | — | Website category circle (D248); app home by slug |
| `CATEGORY_READY_TO_COOK` | 1:1 | none (four cleaned, scored mackerel on a banana leaf; FINAL, D294) | — | — | Website category circle (D248); app home by slug |
| `CATEGORY_MARINATED` | 1:1 | none (masala-marinated fish pieces, curry leaves; to regenerate, D294) | — | — | Website category circle (D248) |
| `CATEGORY_FISH_EGGS` | 1:1 | none (a bowl of orange fish roe; FINAL, D294) | — | — | Website category circle (D248); app home by slug |
| `CATEGORY_READY_TO_EAT` | 1:1 | none (a teal bowl of Andhra fish curry; FINAL, D294) | — | — | Website category circle (D248); app home by slug |
| `WEB_HOME_HERO_HARBOUR` | 20:7 | `FISHMONGER` (Padma in the left third of a sunrise harbour) | — | — | Website home hero background, optional (D248) |
| `SIGN_IN_COAST` | 3:2 | none (two trawlers at a stone jetty, palms, sun, gulls, teal water) | — | — | Sign-in header, right side (D286) |

The twelve onboarding pictures are all `TRANSPARENT` PNGs: save them as
`docs/design/characters/generated/onboarding_tile_<name>.png` / `onboarding_banner_<name>.png`. Until they are
bundled the app draws its own stand-ins (a pastel blob with an icon for a tile, a soft wave with a simple drawn
seafood scene for a banner); the web uses the SVG placeholders. All twelve finals arrived on 2026-10-24 (the
cash / UPI tile redrawn without real notes): the mobile app bundles copies resized with `sips` (tiles 512 × 512,
banners 1024 × 512, alpha kept), so no onboarding tile or banner shows a stand-in any more; the package keeps them
PLACEHOLDER until `import` + `optimize` bring them under 600 KB (see `characters/README.md`).

The three home pictures are `TRANSPARENT` too. The owner's `home_hero_catch.png` (1536 × 1024),
`home_card_cleaned.png` (1254 × 1254) and `home_card_rider.png` (Ravi waving on the scooter) are in
`docs/design/characters/generated/`; the mobile app bundles copies resized with `sips` (hero 1024 wide, cards 512,
alpha kept) and the package keeps all three PLACEHOLDER for the same reason.

The category pictures (`category_fish`, `category_prawns`, `category_crabs`, `category_squid`,
`category_shellfish`) and the sample banners (`sample_banner_seer_steaks`, `sample_banner_prawns`, 1774 × 887)
are data pictures, not app slots: they belong to categories and home offers, which the server sends. The sample
loader stores the committed WebP copy of a category or banner key (`backend/data/sample/images/<path>`, D201) and
draws a placeholder only for a key without a file.

Product pictures are data pictures too (D249): photo-real, not cartoon, so they do not follow the character style
above. The sample loader uploads `backend/data/sample/images/products/<slug>/<n>.webp` (n = 1–4, 1 = main) through
the normal product image pipeline, each with its own alt text from `images.json`, and falls back to the drawn
placeholder only for a product with no file. Brief: square ≥ 1024 px, soft daylight, slightly above, warm sand
`#FBF4E8` or transparent background, no hands or people, no text, the food kept in the middle so the 3:2 frame
(D193) crops nothing important. The owner hands them to the PM (PNG or JPG is fine); the PM saves them as
`docs/design/products/<slug>/<n>.png|jpg` (`sips -Z 1280`) and the `Sample images` workflow makes the 1280 px WebP
(`tools/make_images.py`). Prompts for all 26 sample products: `prompts-product-pictures-2026-10.md`.

The five `CATEGORY_*` slots (D248) cover the categories the sample data has no picture for. They are object-only
`TRANSPARENT` pictures in the look of `backend/data/sample/images/categories/prawns-shrimp.webp` (photo-real seafood,
soft shadow). Finals go into the slots at 1024 × 1024. A 512 × 512 WebP copy of each goes to
`backend/data/sample/images/categories/<slug>.webp` (`fillets-steaks`, `ready-to-cook`, `marinated`, `fish-eggs`,
`ready-to-eat`) once the owner sends them; until then the website shows the fish mark for those categories.
Four arrived on 2026-10-29 and are FINAL (D294): fillets and steaks, ready to cook, fish eggs and ready to eat. Two
of them (fillets and steaks, ready to cook) came with a grey and white checkerboard painted into the pixels instead
of transparency; the checkerboard was cut away (a flood fill from the border over the two checker tones, one
enclosed pocket by the ready-to-cook tail cleared by hand, the 1 px blended rim dropped and the edge softened), and
the cut-outs were checked on dark and light backgrounds. The two with real transparency were made fully opaque
inside (alpha 215 and over) and their faint fringe (alpha under 40) cleared. `generated/` holds the cleaned
1024 × 1024 cut-outs; the package holds 256-colour PNGs (260–400 KB); the sample set and the app hold 512 × 512
WebPs (43–60 KB). The app's home picks them by slug words (`BundledPictures.category`, D220). `CATEGORY_MARINATED`
stays a placeholder: the owner's picture has the checkerboard painted inside the top fish steak's masala too, which
no cut-out can remove, so it must be regenerated with real transparency (or a plain white background).
`WEB_HOME_HERO_HARBOUR` is a `SCENE` at 20:7. While it is a placeholder the website draws its own sky; once it is
FINAL the home passes `/illustrations/<file>` as the hero's `heroBackground`. Prompts ready to paste:
`docs/design/prompts-web-home-2026-10-28.md`.

`LOGIN_OTP` is a chest-up portrait of Ammamma for the round sign-in avatar (D282); prompt:
`docs/design/prompts-app-signin-2026-10-29.md`. The owner's final arrived on 2026-10-29 and is FINAL (D285): the
original (1254 × 1254) is in `generated/login_otp.png`; the package holds a 1024 × 1024 copy framed for a circle
(the portrait scaled to 86 % and set on the bottom edge, so the face is centred and the bun clear of the rim) and
reduced to a 256-colour PNG with transparency (about 250 KB, under the 600 KB limit); the app bundles the same
framing at 512 × 512 (full colour, about 230 KB). The website (`AvatarCircle`, cover) and the app (`AvatarCircle`,
`FreshAvatarBadge`, contain) show the whole square in their circle.

`SIGN_IN_COAST` (D286) is the harbour on the right of the app's sign-in header: a `TRANSPARENT` 3:2 object-only
picture whose top-left fades out. The owner's original (1536 × 1024) is in `generated/sign_in_coast.png`; the
package holds a 1024 × 682 256-colour PNG (about 200 KB) and the app an 800 × 533 copy (about 130 KB). Until it is
bundled the header draws `FreshCoastScene`.

`ORDER_PLACED` (D291) is the owner's waist-up Padma: a thumbs-up with one gloved hand, a big seer fish across her
apron, the picture ending at her waist. The original (1254 × 1254) is in `generated/order_placed.png`; its
almost-opaque cut-out (94–99 %) was made fully opaque inside the figure and the faint fringe cleared. The package
holds a 1024 × 1024 256-colour PNG (about 300 KB) and the app a 600 × 600 copy (about 115 KB). No reframing: on the
app's order-placed header the square stands at the right edge, its bottom 12 px (the cut at her waist) under the
ticket card, and the status and thank-you line end left of it so her thumbs-up is never under text. The website's
order-placed page and the app's tracking card and orders list show it in a circle.

These finals were brought under the limit without `pnpm` (company laptop): `sips` resized them and a small
standard-library Python script framed and colour-reduced them (median cut, dithered), the job `import` and
`optimize` would do.

Admin uses only `ADMIN_*`, `ERROR_GENERIC` and `NOT_FOUND`, at size `sm` (160 px) so dense screens stay dense.

## 4. Components (`@fresh/ui`)

| Component | Use | Key props |
|---|---|---|
| `Illustration` | One slot's picture with alt text and intrinsic size | `slot`, `size` (`xs` 96 · `sm` 160 · `md` 240 · `lg` 320 · `xl` 480 · `fill`), `priority`, `decorative`, `float`, `fit` (`auto` · `cover` · `contain`) |
| `IllustrationProvider` | Change where files load from | `baseUrl` (default `/illustrations`) |
| `EmptyState` | Empty lists and pages | `slot`, `illustrationSize`, `illustrationDecorative` (default true), `headingLevel` |
| `HeroBanner` | Home or onboarding hero; text never on the picture | `title`, `description`, `actions`, `slot` (default `HOME_HERO`), `stickers`, `reverse`, `headingLevel` |
| `FeatureCard` | Benefit card: big picture, chunky headline, one line. **No numbers** | `slot`, `title`, `description`, `tone` (`sand` · `teal` · `ice` · `coral`), `headingLevel` |
| `PolaroidCard` | Photo card with ink border, sand block shadow, stickers on corners | `slot`, `caption`, `stickers[{ label, tone, corner }]`, `tilt` (−2.5° on wide screens) |
| `Sticker` | Short label, Baloo 2 800, ink outline and offset shadow | `tone` (`coral` · `mango` · `teal` · `paper`), `size` (`sm` · `md`), `tilt` (`left` · `right` · `none`) |
| `HelpHero` | Help page header | `title`, `description`, `actions`, `slot` (default `HELP_SUPPORT`), `circleTone` (`mango` · `teal` · `coral` · `sand`) |
| `ReceiptCard` | Order summary as a paper receipt with scalloped edge | `title`, `meta`, `sections` (lines of `{ label, value, description, tone }`), `total`, `note`, `footer` |

`ReceiptCard` shows values exactly as passed: format server amounts with `formatRupees`; it never adds lines up.
Helpers: `resolveIllustration(slot, baseUrl, size)` and `isSceneIllustration(slot)`.

Motion: `float` and the polaroid tilt run only with `motion-safe`; nothing moves under reduced motion. Scene
images never float.

### In an app

```json
{ "scripts": { "dev": "fresh-illustrations-copy public/illustrations && vite", "build": "fresh-illustrations-copy public/illustrations && vite build" } }
```

Import `@fresh/ui/fonts.css` once (self-hosted Baloo 2 and Inter), or load the same fonts with `next/font` and set
`--fo-font-display`. Add `public/illustrations/` to `.gitignore`.

Mobile: `pnpm --filter @fresh/illustrations sync-mobile` copies the files into `apps/mobile/assets/illustrations/` and
writes `lib/core/illustrations/illustration_slots.g.dart` (`IllustrationSlot`, `IllustrationAsset` with `asset`,
`placeholder`, `alt`, `aspectRatio`, `transparent`, `scene`, `isFinal`). Show `scene` assets with `BoxFit.cover` in a
`ClipRRect`.

## 5. Swapping in a final image

1. The owner saves the image as `docs/design/characters/generated/<slot_lower_snake>.jpg` (or `.png` when
   transparent) and adds it to the table in `characters/README.md`.
2. `pnpm --filter @fresh/illustrations run import` copies it into the package and marks the slot FINAL with its size
   and background. (`run` is required: `pnpm import` is a different, built-in pnpm command.)
3. Check the slot's `alt` matches the picture.
4. If `import` lists a problem (too heavy, wrong shape), run `pnpm --filter @fresh/illustrations optimize`.
5. `pnpm --filter @fresh/illustrations validate`, then `pnpm build`; mobile: `sync-mobile`.
6. Commit the image and `manifest.json` together.
