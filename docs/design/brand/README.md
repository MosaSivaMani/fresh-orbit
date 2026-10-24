# Brand: logo

| File | What |
|---|---|
| `logo-master.png` | Owner's logo (flat style: fish, hen and crab in a blue wave, "Fresh Orbitt" in navy and blue, green leaf), 1774 × 887 PNG on white, received 2026-10-24 (D119). Never edit; make new sizes from it |
| `logo-640.png` | 640 px wide copy for docs, mockups, emails |
| `mark.png` | The fish, hen and crab cropped from the master, 560 × 560 on white. Interim small mark: website header and footer (`apps/web/public/brand/mark.png`, 128 px), later favicon and app icon |

An earlier detailed 3D-style logo (fish, crab, chicken with splashes) was replaced by this one the same day at the owner's word.

## Colours in the logo
Navy `#0E3F57` ("Fresh"), sea blue `#1B9AD0` ("Orbit", the wave), coral-red `#E8512A` (hen, crab), leaf green `#6AA341`.
They sit well with the app palette (D88): coral matches our coral, the blue goes with teal.

## Where it is used
- Full logo where there is room: website hero and footer, app splash and sign-in, emails, invoices, social posts.
- The mark in a white circle with an ink ring next to the "Fresh Orbit" wordmark: website header and footer (`components/logo.tsx`).
- Mobile splash screen: full logo on a white card on the teal block (`apps/mobile/assets/brand/logo.png`, the 640 px copy).
- Still to do: favicon and Android/iOS app icon from `mark.png` (the owner may give a clean square version with transparency).

## Open questions for the owner
- The brand name is **"Fresh Orbitt"** (double t), as the logo reads; owner confirmed 2026-10-24 (D170). Every name a
  customer or staff member sees says "Fresh Orbitt". Code names (`FreshOrbit`, `fresh-orbit`, `@fresh/*`) and the repository
  keep the single t, because renaming them changes nothing anyone sees.
- The hen in the logo stays: chicken will be sold, and vegetables later (D171).
- The logo shows a **hen**: the catalogue so far is seafood only. Chicken needs its own categories, prices and storage rules
  (configuration, rule 7) before it is sold.
