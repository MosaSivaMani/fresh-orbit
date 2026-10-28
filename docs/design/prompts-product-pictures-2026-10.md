# Prompts: product pictures (2026-10)

Real pictures for the 26 sample products, 2 to 3 per product (78 in all). They replace the drawn
placeholder on the website, app and admin product pages: the sample loader uploads every committed picture through
the normal image pipeline (320 / 640 / 1280 px WebP renditions, like an admin upload) and falls back to the
placeholder only for a product with no picture file (D249, `docs/product/catalog.md` §Pictures).

## How to hand them over

1. Generate each picture from its prompt with a tool whose licence allows commercial use (as for the illustrations,
   `docs/design/illustrations.md`). Square 1:1, at least 1024 × 1024 px; PNG or JPG is fine.
2. Name each file `<slug>-<n>.png` (or `.jpg`), for example `seer-fish-1.png`, and send them to the PM. Picture 1
   is the main picture (cover on cards and the first gallery slide); 3 is optional.
3. The PM saves them as `docs/design/products/<slug>/<n>.png|jpg` (resized to 1280 px on the longest side with
   `sips -Z 1280` so the repository stays small) and runs the `Sample images` workflow; `make_images.py` makes
   `backend/data/sample/images/products/<slug>/<n>.webp` (1280 px, quality 82) on the CI runner and the result is
   committed. The validator checks every product picture has its alt text in `images.json` and stays under 600 KB.
4. The demo picks them up on the next sample load (`--purge-sample` then `--sample` for an existing database:
   the loader skips products that already exist). Staff can also upload, reorder, set the main picture or delete
   pictures on the admin product page at any time.

Every prompt keeps the same look so the pictures sit together: photo-real, soft daylight, slightly above, warm
sand (#FBF4E8) or transparent background, no hands or people, no text. Products appear on screen in a 3:2 frame
(D193), so each prompt keeps the food in the middle with space above and below.

Alt text: the loader uses the alt text in `backend/data/sample/images.json` (`pictures[].altText`); it is repeated
below so the picture can be checked against it.

## Index

| # | Product | Slug | Preparations sold | Pictures |
|---|---|---|---|---|
| 1 | Seer Fish (Vanjaram) | `seer-fish` | `CLEANED`, `CURRY_CUT`, `STEAK` (default), `FRY_CUT`, `FILLET_SKINLESS` | 3 |
| 2 | Silver Pomfret | `silver-pomfret` | `WHOLE`, `CLEANED` (default), `CURRY_CUT` | 3 |
| 3 | Rohu | `rohu` | `CLEANED`, `CURRY_CUT` (default), `STEAK` | 3 |
| 4 | Katla | `katla` | `CLEANED`, `CURRY_CUT` (default), `STEAK` | 3 |
| 5 | Yellowfin Tuna | `yellowfin-tuna` | `STEAK` (default), `BONELESS_CUBES` | 3 |
| 6 | Indian Mackerel (Bangda) | `indian-mackerel` | `WHOLE`, `CLEANED` (default), `CLEANED_HEADLESS` | 3 |
| 7 | Oil Sardine (Mathi) | `oil-sardine` | `WHOLE`, `CLEANED` (default), `CLEANED_HEADLESS` | 3 |
| 8 | Red Snapper | `red-snapper` | `CLEANED`, `CURRY_CUT` (default), `FILLET` | 3 |
| 9 | Barramundi (Bhetki) | `barramundi` | `CLEANED`, `CURRY_CUT`, `FILLET_SKINLESS` (default) | 3 |
| 10 | Tiger Prawns | `tiger-prawns` | `WHOLE`, `DEVEINED_SHELL_ON`, `PEELED_TAIL_ON` (default), `PEELED_DEVEINED` (default), `BUTTERFLY` | 3 |
| 11 | White Prawns | `white-prawns` | `WHOLE`, `PEELED_DEVEINED` (default) | 3 |
| 12 | Scampi (Freshwater Prawn) | `scampi` | `WHOLE` (default), `DEVEINED_SHELL_ON`, `PEELED_DEVEINED` | 3 |
| 13 | Mud Crab | `mud-crab` | `WHOLE`, `CRAB_CLEANED_CUT` (default) | 3 |
| 14 | Blue Swimmer Crab | `blue-swimmer-crab` | `WHOLE`, `CRAB_CLEANED_CUT` (default) | 3 |
| 15 | Squid | `squid` | `WHOLE`, `SQUID_CLEANED` (default), `SQUID_RINGS` | 3 |
| 16 | Green Mussels | `green-mussels` | `WHOLE` (default) | 3 |
| 17 | Clams | `clams` | `WHOLE` (default) | 3 |
| 18 | Spiny Lobster | `spiny-lobster` | `WHOLE` (default), `BUTTERFLY` | 3 |
| 19 | Basa Fillet | `basa-fillet` | `FILLET_SKINLESS` (default) | 3 |
| 20 | Atlantic Salmon Fillet | `atlantic-salmon-fillet` | `FILLET` (default) | 3 |
| 21 | Anchovy (Nethili) | `anchovy` | `WHOLE`, `CLEANED_HEADLESS` (default) | 3 |
| 22 | Ribbon Fish | `ribbon-fish` | `CLEANED_HEADLESS`, `CURRY_CUT` (default) | 3 |
| 23 | Indian Halibut | `indian-halibut` | `CLEANED`, `CURRY_CUT`, `STEAK` (default) | 3 |
| 24 | Tongue Sole | `tongue-sole` | `CLEANED` (default), `FILLET_SKINLESS` | 3 |
| 25 | Seer Fish Steaks (Pack) | `seer-fish-steaks` | `STEAK` (default) | 3 |
| 26 | Andhra Fry Marinated Prawns | `marinated-prawns-andhra-fry` | `PEELED_DEVEINED` (default) | 3 |

## 1. Seer Fish (Vanjaram) (`seer-fish`)

**1. As sold, raw** → `backend/data/sample/images/products/seer-fish/1.webp` (send as `seer-fish-1.png`)

Alt text: "Seer fish, whole on crushed ice"

```text
Photo-real food photography of one whole fresh seer fish (king mackerel), long and silver with faint wavy grey bars and a bright clear eye, lying diagonally on a bed of crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/seer-fish/2.webp` (send as `seer-fish-2.png`)

Alt text: "Seer fish steaks, cleaned and cut about 1 cm thick"

```text
Photo-real food photography of six raw seer fish steaks about 1 cm thick with the round centre bone in, cleaned, firm white-pink flesh with silver skin edges, in a neat overlapping row on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/seer-fish/3.webp` (send as `seer-fish-3.png`)

Alt text: "Andhra seer fish fry (vanjaram vepudu)"

```text
Photo-real food photography of Andhra-style seer fish fry: three pan-fried seer fish steaks coated in a red chilli masala, crisp at the edges, with fried curry leaves, lemon wedges and thin onion rings on a round steel plate. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 2. Silver Pomfret (`silver-pomfret`)

**1. As sold, raw** → `backend/data/sample/images/products/silver-pomfret/1.webp` (send as `silver-pomfret-1.png`)

Alt text: "Silver pomfret, whole on crushed ice"

```text
Photo-real food photography of three whole fresh silver pomfrets, flat, oval and pearly silver with a tiny mouth, side by side on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/silver-pomfret/2.webp` (send as `silver-pomfret-2.png`)

Alt text: "Silver pomfret, cleaned and scored, with curry-cut pieces"

```text
Photo-real food photography of two cleaned raw silver pomfrets with fins trimmed and shallow diagonal scores on the body, beside a few raw pomfret curry-cut pieces, on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/silver-pomfret/3.webp` (send as `silver-pomfret-3.png`)

Alt text: "Andhra pomfret tawa fry"

```text
Photo-real food photography of Andhra-style pomfret tawa fry: one whole pomfret shallow-fried in a red chilli and garlic masala, on a banana leaf on a round plate, with onion rings and a lemon wedge. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 3. Rohu (`rohu`)

**1. As sold, raw** → `backend/data/sample/images/products/rohu/1.webp` (send as `rohu-1.png`)

Alt text: "Rohu, whole on crushed ice"

```text
Photo-real food photography of one whole fresh rohu carp of about 2 kg, with neat reddish-silver scales and a grey-green back, on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/rohu/2.webp` (send as `rohu-2.png`)

Alt text: "Rohu curry-cut pieces, scales removed"

```text
Photo-real food photography of raw rohu curry-cut pieces with the bone in, cleaned and scales removed, pink-white flesh and silver skin, piled neatly on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/rohu/3.webp` (send as `rohu-3.png`)

Alt text: "Andhra rohu fish curry (chepala pulusu)"

```text
Photo-real food photography of Andhra chepala pulusu: rohu pieces in a glossy, tangy red tamarind gravy in a small clay pot, garnished with curry leaves and a few green chillies. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 4. Katla (`katla`)

**1. As sold, raw** → `backend/data/sample/images/products/katla/1.webp` (send as `katla-1.png`)

Alt text: "Katla, whole on crushed ice"

```text
Photo-real food photography of one whole fresh katla carp with a large head, an upturned mouth and a broad silver-grey scaled body, on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/katla/2.webp` (send as `katla-2.png`)

Alt text: "Katla steaks and curry-cut pieces"

```text
Photo-real food photography of raw katla steaks and curry-cut pieces with the bone in, cleaned and scales removed, arranged on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/katla/3.webp` (send as `katla-3.png`)

Alt text: "Andhra katla fish curry"

```text
Photo-real food photography of Andhra-style katla fish curry: thick katla pieces in an onion, tomato and red chilli gravy in a small clay pot, topped with coriander leaves. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 5. Yellowfin Tuna (`yellowfin-tuna`)

**1. As sold, raw** → `backend/data/sample/images/products/yellowfin-tuna/1.webp` (send as `yellowfin-tuna-1.png`)

Alt text: "Yellowfin tuna loin, raw, on crushed ice"

```text
Photo-real food photography of a thick raw yellowfin tuna loin with deep red flesh and a strip of dark silver skin along one side, on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/yellowfin-tuna/2.webp` (send as `yellowfin-tuna-2.png`)

Alt text: "Yellowfin tuna steaks and boneless cubes"

```text
Photo-real food photography of four raw yellowfin tuna steaks about 2 cm thick, deep red, beside a small heap of raw boneless tuna cubes, on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/yellowfin-tuna/3.webp` (send as `yellowfin-tuna-3.png`)

Alt text: "Andhra tuna pepper fry"

```text
Photo-real food photography of Andhra-style tuna pepper fry: browned boneless tuna cubes tossed with crushed black pepper, curry leaves and sliced onion in a black cast-iron pan. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 6. Indian Mackerel (Bangda) (`indian-mackerel`)

**1. As sold, raw** → `backend/data/sample/images/products/indian-mackerel/1.webp` (send as `indian-mackerel-1.png`)

Alt text: "Indian mackerel (bangda), whole on crushed ice"

```text
Photo-real food photography of eight whole fresh Indian mackerels with blue-green backs, thin golden stripes and silver bellies, in a neat row on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/indian-mackerel/2.webp` (send as `indian-mackerel-2.png`)

Alt text: "Indian mackerel, cleaned, some with heads removed"

```text
Photo-real food photography of raw Indian mackerels cleaned with gills and gut removed, a few with the heads cut off, the bodies lightly scored, on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/indian-mackerel/3.webp` (send as `indian-mackerel-3.png`)

Alt text: "Andhra mackerel masala fry"

```text
Photo-real food photography of Andhra-style mackerel masala fry: four whole mackerels coated in red chilli masala and pan-fried until crisp, on a banana leaf with lemon wedges and curry leaves. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 7. Oil Sardine (Mathi) (`oil-sardine`)

**1. As sold, raw** → `backend/data/sample/images/products/oil-sardine/1.webp` (send as `oil-sardine-1.png`)

Alt text: "Oil sardines (mathi), whole on crushed ice"

```text
Photo-real food photography of a heap of about twenty small whole oil sardines, silver with a blue-green back, glistening on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/oil-sardine/2.webp` (send as `oil-sardine-2.png`)

Alt text: "Oil sardines, cleaned with heads removed"

```text
Photo-real food photography of raw oil sardines cleaned with heads and gut removed, laid in two neat rows on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/oil-sardine/3.webp` (send as `oil-sardine-3.png`)

Alt text: "Andhra sardine fry (kavalu vepudu)"

```text
Photo-real food photography of Andhra-style sardine fry: crisp pan-fried oil sardines coated in red chilli masala on a banana leaf, with fried curry leaves and a lemon wedge. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 8. Red Snapper (`red-snapper`)

**1. As sold, raw** → `backend/data/sample/images/products/red-snapper/1.webp` (send as `red-snapper-1.png`)

Alt text: "Red snapper, whole on crushed ice"

```text
Photo-real food photography of one whole fresh red snapper with bright pink-red skin, a pale belly and a clear eye, on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/red-snapper/2.webp` (send as `red-snapper-2.png`)

Alt text: "Red snapper curry-cut pieces and fillets"

```text
Photo-real food photography of raw red snapper curry-cut pieces with the bone in beside two raw skin-on red snapper fillets, on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/red-snapper/3.webp` (send as `red-snapper-3.png`)

Alt text: "Andhra red snapper curry"

```text
Photo-real food photography of Andhra fish curry: red snapper pieces in a tangy tamarind and tomato gravy in a small clay pot, garnished with curry leaves. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 9. Barramundi (Bhetki) (`barramundi`)

**1. As sold, raw** → `backend/data/sample/images/products/barramundi/1.webp` (send as `barramundi-1.png`)

Alt text: "Barramundi (bhetki), whole on crushed ice"

```text
Photo-real food photography of one whole fresh barramundi with large silver scales, a dark grey back and a pointed head, on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/barramundi/2.webp` (send as `barramundi-2.png`)

Alt text: "Barramundi skinless, boneless fillets"

```text
Photo-real food photography of two raw skinless, boneless barramundi fillets, pearly white and moist, with a few curry-cut pieces beside them, on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/barramundi/3.webp` (send as `barramundi-3.png`)

Alt text: "Andhra barramundi fry"

```text
Photo-real food photography of Andhra-style barramundi fry: golden pan-fried fillet pieces coated in a red chilli and garlic masala, on a banana leaf with onion rings and lemon. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 10. Tiger Prawns (`tiger-prawns`)

**1. As sold, raw** → `backend/data/sample/images/products/tiger-prawns/1.webp` (send as `tiger-prawns-1.png`)

Alt text: "Tiger prawns, whole with head and shell, on crushed ice"

```text
Photo-real food photography of about ten large raw tiger prawns, head and shell on, with dark grey-blue stripes, on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/tiger-prawns/2.webp` (send as `tiger-prawns-2.png`)

Alt text: "Tiger prawns, peeled and deveined, some with tails on"

```text
Photo-real food photography of raw tiger prawns peeled and deveined, half of them with the tail on, in a neat curved row on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/tiger-prawns/3.webp` (send as `tiger-prawns-3.png`)

Alt text: "Andhra prawn fry (royyala vepudu)"

```text
Photo-real food photography of Andhra royyala vepudu: tiger prawns dry-roasted with red chilli, garlic and curry leaves in a black cast-iron kadai. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 11. White Prawns (`white-prawns`)

**1. As sold, raw** → `backend/data/sample/images/products/white-prawns/1.webp` (send as `white-prawns-1.png`)

Alt text: "White prawns, whole with head and shell, on crushed ice"

```text
Photo-real food photography of a heap of medium raw white prawns, translucent grey-white with head and shell on, on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/white-prawns/2.webp` (send as `white-prawns-2.png`)

Alt text: "White prawns, peeled and deveined"

```text
Photo-real food photography of raw white prawns peeled and deveined, plump and pale, in a shallow steel bowl set on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/white-prawns/3.webp` (send as `white-prawns-3.png`)

Alt text: "Andhra prawn curry (royyala koora)"

```text
Photo-real food photography of Andhra royyala koora: white prawns in a thick onion, tomato and red chilli gravy in a small clay pot, with curry leaves and coriander. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 12. Scampi (Freshwater Prawn) (`scampi`)

**1. As sold, raw** → `backend/data/sample/images/products/scampi/1.webp` (send as `scampi-1.png`)

Alt text: "Scampi, whole with long claws, on crushed ice"

```text
Photo-real food photography of four large raw freshwater scampi (giant river prawns) with big heads and long thin blue claws, on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/scampi/2.webp` (send as `scampi-2.png`)

Alt text: "Scampi, deveined in the shell and peeled"

```text
Photo-real food photography of raw scampi deveined with the shell on beside a few peeled and deveined scampi tails, on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/scampi/3.webp` (send as `scampi-3.png`)

Alt text: "Andhra scampi masala"

```text
Photo-real food photography of Andhra-style scampi masala: whole scampi with claws in a thick red onion-chilli masala in a wide black kadai, with curry leaves. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 13. Mud Crab (`mud-crab`)

**1. As sold, raw** → `backend/data/sample/images/products/mud-crab/1.webp` (send as `mud-crab-1.png`)

Alt text: "Mud crab, whole, on crushed ice"

```text
Photo-real food photography of two whole raw mud crabs with dark olive-green shells and large heavy claws, on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/mud-crab/2.webp` (send as `mud-crab-2.png`)

Alt text: "Mud crab, cleaned and cut, claws cracked"

```text
Photo-real food photography of one raw mud crab cleaned and cut into four pieces with the claws lightly cracked, on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/mud-crab/3.webp` (send as `mud-crab-3.png`)

Alt text: "Andhra crab curry (peetala pulusu)"

```text
Photo-real food photography of Andhra peetala pulusu: mud crab pieces and claws in a thick, spicy red gravy in a wide clay pot, garnished with curry leaves. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 14. Blue Swimmer Crab (`blue-swimmer-crab`)

**1. As sold, raw** → `backend/data/sample/images/products/blue-swimmer-crab/1.webp` (send as `blue-swimmer-crab-1.png`)

Alt text: "Blue swimmer crabs, whole on crushed ice"

```text
Photo-real food photography of four whole raw blue swimmer crabs with blue legs, speckled blue-brown shells and long claws, on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/blue-swimmer-crab/2.webp` (send as `blue-swimmer-crab-2.png`)

Alt text: "Blue swimmer crabs, cleaned and cut in halves"

```text
Photo-real food photography of raw blue swimmer crabs cleaned, top shells removed and cut in halves, claws on, on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/blue-swimmer-crab/3.webp` (send as `blue-swimmer-crab-3.png`)

Alt text: "Andhra crab rasam (peetala charu)"

```text
Photo-real food photography of Andhra peetala charu: a thin, peppery tomato and tamarind crab rasam in a steel bowl with two crab pieces showing, curry leaves on top. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 15. Squid (`squid`)

**1. As sold, raw** → `backend/data/sample/images/products/squid/1.webp` (send as `squid-1.png`)

Alt text: "Squid, whole on crushed ice"

```text
Photo-real food photography of five whole raw squid with smooth purple-speckled skin and short tentacles, on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/squid/2.webp` (send as `squid-2.png`)

Alt text: "Squid, cleaned tubes and rings"

```text
Photo-real food photography of cleaned raw squid tubes, pure white, beside a heap of raw squid rings, on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/squid/3.webp` (send as `squid-3.png`)

Alt text: "Andhra squid roast"

```text
Photo-real food photography of Andhra-style squid roast: squid rings roasted with black pepper, red chilli, onion and curry leaves in a black cast-iron kadai. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 16. Green Mussels (`green-mussels`)

**1. As sold, raw** → `backend/data/sample/images/products/green-mussels/1.webp` (send as `green-mussels-1.png`)

Alt text: "Green mussels in the shell, on crushed ice"

```text
Photo-real food photography of a heap of fresh green mussels in their bright green-and-brown shells, on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/green-mussels/2.webp` (send as `green-mussels-2.png`)

Alt text: "Green mussels, scrubbed and cleaned in the shell"

```text
Photo-real food photography of scrubbed green mussels with the beards removed, wet and glistening, a few opened to show the orange meat, on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/green-mussels/3.webp` (send as `green-mussels-3.png`)

Alt text: "Andhra mussel fry"

```text
Photo-real food photography of Andhra-style mussel fry: mussel meat in half shells roasted in a red chilli and onion masala, on a round steel plate with curry leaves. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 17. Clams (`clams`)

**1. As sold, raw** → `backend/data/sample/images/products/clams/1.webp` (send as `clams-1.png`)

Alt text: "Clams in the shell, on crushed ice"

```text
Photo-real food photography of a heap of fresh hard-shell clams with cream and brown patterned shells, on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/clams/2.webp` (send as `clams-2.png`)

Alt text: "Clams, washed and ready to cook"

```text
Photo-real food photography of washed raw clams in the shell in a shallow steel bowl of clear water, a few opened to show the meat, set on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/clams/3.webp` (send as `clams-3.png`)

Alt text: "Andhra clam fry"

```text
Photo-real food photography of Andhra-style clam fry: clams in the shell tossed with grated coconut, onion, red chilli and curry leaves in a black cast-iron kadai. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 18. Spiny Lobster (`spiny-lobster`)

**1. As sold, raw** → `backend/data/sample/images/products/spiny-lobster/1.webp` (send as `spiny-lobster-1.png`)

Alt text: "Spiny lobster, whole on crushed ice"

```text
Photo-real food photography of one whole raw spiny lobster with a mottled brown-orange spiny shell and long antennae, on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/spiny-lobster/2.webp` (send as `spiny-lobster-2.png`)

Alt text: "Spiny lobster, butterflied"

```text
Photo-real food photography of one raw spiny lobster split lengthwise (butterflied), showing the white meat in the shell, on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/spiny-lobster/3.webp` (send as `spiny-lobster-3.png`)

Alt text: "Andhra lobster masala grill"

```text
Photo-real food photography of Andhra-style lobster masala grill: a butterflied spiny lobster grilled with a red chilli, garlic and butter masala, on a banana leaf with lemon wedges. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 19. Basa Fillet (`basa-fillet`)

**1. As sold, raw** → `backend/data/sample/images/products/basa-fillet/1.webp` (send as `basa-fillet-1.png`)

Alt text: "Basa fillets, skinless and boneless"

```text
Photo-real food photography of three raw skinless, boneless basa fillets, smooth and pale white, slightly overlapping on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/basa-fillet/2.webp` (send as `basa-fillet-2.png`)

Alt text: "Basa fillet cut into portions"

```text
Photo-real food photography of two raw basa fillets cut into even finger-sized portions, laid in neat rows on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/basa-fillet/3.webp` (send as `basa-fillet-3.png`)

Alt text: "Andhra chilli basa fry"

```text
Photo-real food photography of Andhra-style chilli fish: golden fried basa pieces tossed with red chilli, garlic, green chillies and curry leaves on a round steel plate. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 20. Atlantic Salmon Fillet (`atlantic-salmon-fillet`)

**1. As sold, raw** → `backend/data/sample/images/products/atlantic-salmon-fillet/1.webp` (send as `atlantic-salmon-fillet-1.png`)

Alt text: "Atlantic salmon fillet portions, skin on"

```text
Photo-real food photography of two raw boneless Atlantic salmon portions, bright orange flesh with fine white lines, one showing the silver skin side, on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/atlantic-salmon-fillet/2.webp` (send as `atlantic-salmon-fillet-2.png`)

Alt text: "Atlantic salmon portion, boneless, on a banana leaf"

```text
Photo-real food photography of one raw boneless skin-on Atlantic salmon portion, close up, flesh side up, on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/atlantic-salmon-fillet/3.webp` (send as `atlantic-salmon-fillet-3.png`)

Alt text: "Pan-seared salmon with Andhra spices"

```text
Photo-real food photography of a pan-seared skin-on salmon portion with a crisp skin and a crust of red chilli, crushed pepper and curry leaves, on a round steel plate with a lemon wedge. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 21. Anchovy (Nethili) (`anchovy`)

**1. As sold, raw** → `backend/data/sample/images/products/anchovy/1.webp` (send as `anchovy-1.png`)

Alt text: "Anchovies (nethili), whole on crushed ice"

```text
Photo-real food photography of a heap of tiny whole silver anchovies with a thin silver stripe, glistening on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/anchovy/2.webp` (send as `anchovy-2.png`)

Alt text: "Anchovies, cleaned with heads removed"

```text
Photo-real food photography of raw anchovies cleaned with heads and gut removed, in a small neat heap on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/anchovy/3.webp` (send as `anchovy-3.png`)

Alt text: "Andhra nethili fry"

```text
Photo-real food photography of Andhra-style anchovy fry: a heap of crisp fried anchovies coated in red chilli masala, with fried curry leaves and a lemon wedge on a banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 22. Ribbon Fish (`ribbon-fish`)

**1. As sold, raw** → `backend/data/sample/images/products/ribbon-fish/1.webp` (send as `ribbon-fish-1.png`)

Alt text: "Ribbon fish, whole on crushed ice"

```text
Photo-real food photography of two long, thin whole ribbon fish, bright metallic silver, laid in a gentle S curve on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/ribbon-fish/2.webp` (send as `ribbon-fish-2.png`)

Alt text: "Ribbon fish, cleaned and curry-cut"

```text
Photo-real food photography of raw ribbon fish cleaned and cut into 5 cm curry pieces, in neat rows on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/ribbon-fish/3.webp` (send as `ribbon-fish-3.png`)

Alt text: "Andhra ribbon fish fry (savalu vepudu)"

```text
Photo-real food photography of Andhra-style ribbon fish fry: pan-fried ribbon fish pieces coated in red chilli masala on a banana leaf, with curry leaves and onion rings. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 23. Indian Halibut (`indian-halibut`)

**1. As sold, raw** → `backend/data/sample/images/products/indian-halibut/1.webp` (send as `indian-halibut-1.png`)

Alt text: "Indian halibut, whole on crushed ice"

```text
Photo-real food photography of one whole fresh Indian halibut, a thick flatfish with a brown-grey upper side and both eyes on top, on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/indian-halibut/2.webp` (send as `indian-halibut-2.png`)

Alt text: "Indian halibut steaks and curry-cut pieces"

```text
Photo-real food photography of raw Indian halibut steaks with the centre bone and a few curry-cut pieces, firm white flesh, on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/indian-halibut/3.webp` (send as `indian-halibut-3.png`)

Alt text: "Andhra halibut fry"

```text
Photo-real food photography of Andhra-style halibut fry: three pan-fried halibut steaks coated in red chilli masala, with curry leaves and lemon wedges on a round steel plate. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 24. Tongue Sole (`tongue-sole`)

**1. As sold, raw** → `backend/data/sample/images/products/tongue-sole/1.webp` (send as `tongue-sole-1.png`)

Alt text: "Tongue sole, whole on crushed ice"

```text
Photo-real food photography of four whole fresh tongue sole, thin tongue-shaped flatfish with light brown skin, side by side on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/tongue-sole/2.webp` (send as `tongue-sole-2.png`)

Alt text: "Tongue sole, cleaned, and skinless fillets"

```text
Photo-real food photography of two cleaned raw tongue sole beside four raw skinless tongue sole fillets, thin and white, on a fresh green banana leaf. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/tongue-sole/3.webp` (send as `tongue-sole-3.png`)

Alt text: "Andhra tongue sole shallow fry"

```text
Photo-real food photography of Andhra-style tongue sole fry: two whole tongue sole shallow-fried in a light red chilli and turmeric masala, on a banana leaf with lemon. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 25. Seer Fish Steaks (Pack) (`seer-fish-steaks`)

**1. As sold, raw** → `backend/data/sample/images/products/seer-fish-steaks/1.webp` (send as `seer-fish-steaks-1.png`)

Alt text: "Seer fish steaks, about 1 cm thick, centre bone in"

```text
Photo-real food photography of eight raw seer fish steaks about 1 cm thick with the round centre bone in, fanned out in a neat overlapping row on crushed ice. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/seer-fish-steaks/2.webp` (send as `seer-fish-steaks-2.png`)

Alt text: "Seer fish steaks, close up on a banana leaf"

```text
Photo-real food photography of three raw seer fish steaks close up on a fresh green banana leaf, showing the firm white-pink flesh, silver skin edge and centre bone. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/seer-fish-steaks/3.webp` (send as `seer-fish-steaks-3.png`)

Alt text: "Andhra seer fish curry (vanjaram pulusu)"

```text
Photo-real food photography of Andhra vanjaram pulusu: seer fish steaks in a tangy red tamarind gravy in a wide clay pot, garnished with curry leaves and green chillies. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

## 26. Andhra Fry Marinated Prawns (`marinated-prawns-andhra-fry`)

**1. As sold, raw** → `backend/data/sample/images/products/marinated-prawns-andhra-fry/1.webp` (send as `marinated-prawns-andhra-fry-1.png`)

Alt text: "Prawns in a red chilli, garlic and curry-leaf marinade"

```text
Photo-real food photography of raw peeled and deveined prawns coated in a thick red chilli, garlic and curry-leaf marinade, in a shallow steel bowl. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**2. Cleaned and cut, as delivered** → `backend/data/sample/images/products/marinated-prawns-andhra-fry/2.webp` (send as `marinated-prawns-andhra-fry-2.png`)

Alt text: "Marinated prawns spread on a banana leaf, ready to fry"

```text
Photo-real food photography of raw marinated prawns, red with chilli and flecked with curry leaves, spread in one layer on a fresh green banana leaf, ready to fry. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). Plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

**3. Cooked Andhra dish (optional)** → `backend/data/sample/images/products/marinated-prawns-andhra-fry/3.webp` (send as `marinated-prawns-andhra-fry-3.png`)

Alt text: "Andhra prawn fry (royyala vepudu)"

```text
Photo-real food photography of the cooked dish: Andhra royyala vepudu, marinated prawns pan-fried until dark red and crisp at the edges, with fried curry leaves in a black cast-iron kadai. Square 1:1 image, at least 1024 × 1024 px. Soft natural daylight, gentle soft shadows, seen from slightly above (about 30 degrees). The plate or pot stands on a plain warm sand background (#FBF4E8), or a transparent background. The food sits in the centre and fills about two thirds of the width, with clear space above and below it, so a 3:2 crop of the middle keeps all of it. Fresh, clean, appetising, true-to-life colours, sharp focus. No hands, no people, no packaging. No text, no letters, no numbers, no logos, no watermark. High resolution.
```
