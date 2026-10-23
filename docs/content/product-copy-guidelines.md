> **DRAFT — for owner review.** House rules for writing product and category text in the admin. The sample catalogue
> (`backend/data/sample/catalog.json`) follows them; its local names, scientific names and yields are drafts that the
> owner must verify before any of it is reused for real products.

# Product copy guidelines

Field limits are enforced by the system (`docs/product/catalog.md` CAT-R1, CAT-R3, CAT-R19): name ≤ 120 characters,
up to 10 local names of ≤ 40 characters each, scientific name and source region ≤ 200, description, storage and
handling text ≤ 2,000.

## 1. Product name

- **Common English trade name first**, then the best-known local name in brackets when customers search for it:
  *Seer Fish (Vanjaram)*, *Indian Mackerel (Bangda)*, *Barramundi (Bhetki)*.
- One species (or one tightly defined group) per product. Size goes into the **variant** name, not the product name:
  product *Tiger Prawns*, variants *Large (20–30 per kg)* and *Jumbo (10–15 per kg)*.
- Name what it is, not how good it is: no "Premium", "Best", "Fresh" in names.
- Pre-cut or packed products say so: *Seer Fish Steaks (Pack)*, *Basa Fillet*. Frozen products say *Frozen* in the
  variant name (*Frozen pack, 500 g net*).
- Avoid names that mean different fish in different places. Examples to handle carefully:
  - *Salmon*: imported Atlantic salmon (*Salmo salar*) is not Indian salmon / Rawas (*Eleutheronema tetradactylum*).
    Use the full name.
  - *Seer / Surmai / Kingfish*: several *Scomberomorus* species are sold under these names; the scientific name decides.
  - *Pomfret*: silver and black pomfret are different products with different prices.
  - *Konju* is used for both scampi and other prawns in Kerala; do not rely on it alone.
- Slug: lowercase, hyphens, from the English name (`seer-fish`). It cannot change once the product is live.

## 2. Local names

- Write them in **Latin script** as customers type them (search works on these): *Vanjaram*, *Neymeen*, *Paplet*.
  [Owner: decide whether to also show names in Telugu, Tamil, Malayalam, Bengali and Devanagari script on the product
  page; the search index would need them too.]
- Include the names used in the cities we serve and by the communities who buy the fish there; for Andhra Pradesh,
  Telugu names first.
- Use the common spelling; add a second spelling only if people really search for it (*Vanjaram* / *Vanjiram* — the
  search already tolerates small typos).
- Record the language of each name (`localNameLanguages` in the sample file: `ta`, `te`, `ml`, `hi`, `mr`, `bn`).
- **Verify every local name with a native speaker or a fisheries reference before publishing.** If you are not sure,
  leave it out; a wrong local name sells the customer a different fish.

## 3. Scientific name

- Genus and species, correctly capitalised: *Scomberomorus commerson*. Check the current accepted name in a fisheries
  database (for example FishBase or SeaLifeBase).
- If a product can be more than one species (for example anchovy), use the main species and say "and related
  species" in the description, or leave the field empty.

## 4. Source type and region

- **Wild** only for wild-caught; **Farmed** for anything from ponds, farms or cages. Never guess.
- Region as specifically as the business can prove: *Bay of Bengal, Andhra Pradesh coast*; *Pond farms, Krishna and
  Godavari delta*; *Imported (country of origin printed on each pack)*.

## 5. Description (2–4 short sentences)

1. What it is like to eat: texture and taste (*firm, mild, few bones*; *oily, full-flavoured*).
2. What it is good for: two or three dishes customers know (*fry, steaks, curry*).
3. Size or count, if it matters: *cut from fish of about 2–4 kg*; *6–10 per kg*.
4. Anything the customer must know: *Frozen pack*, *Contains: crustaceans*, *sold by net weight after cutting*.

Write plainly, in the second person when giving advice, without exclamation marks.

## 6. Claims we do not make

Do not write any claim the business cannot prove with records or certificates. In particular, never:

| Do not write | Why | Say instead (only if true) |
|---|---|---|
| "Fresh catch of the day", "caught this morning", "100 % fresh" | The batch record may show a catch date of yesterday; freshness is not a guarantee we can print | "Bought from the harbour auction and kept on ice at 0–4 °C" (if that is the process) |
| "Chemical-free", "formalin-free", "no preservatives" | Needs lab testing on every batch | Nothing, unless every batch is tested and the reports are kept |
| "Organic", "antibiotic-free", "sustainably sourced" | Need certification | Name the certificate and its number, if any |
| Health claims ("boosts immunity", "good for the heart") | Regulated health claims | "A source of protein" only if allowed by FSSAI labelling rules [Legal review] |
| "Best price", "cheapest in town" | Unprovable comparison | — |
| "Boneless" for bone-in cuts | Misleading | "Few bones", "centre bone in" |

## 7. Storage instructions (use the standard sentences)

| Kind | Text |
|---|---|
| Fresh / chilled | Keep refrigerated at 0–4 °C and cook within 24 hours of delivery, or freeze at −18 °C or below on the day of delivery. |
| Frozen | Keep frozen at −18 °C or below. Thaw in the refrigerator, not at room temperature. Do not refreeze after thawing. |
| Live shellfish (crab, mussels, clams) | Refrigerate in the box at 2–6 °C with a damp cloth on top; do not seal airtight or keep in water. Cook the same day. Discard any with cracked shells or that stay open when tapped. |
| Marinated (chilled) | Keep refrigerated at 0–4 °C and cook within 24 hours of delivery. Do not freeze after opening. |

[Owner: confirm these times with your food-safety adviser.]

## 8. Handling notes

One or two practical tips for cooking or preparing: *Steaks include the centre bone*; *Cook squid under 2 minutes or
over 30*; *Soak clams in salted water for 20 minutes*. No marketing.

## 9. Variants, preparations and yields

- KG variants are sold by **net weight after preparation**; the default order step is 250 g.
- Write preparation-specific facts in the description, not in the variant name.
- Yields (`yieldBp`) are set from **test cuts** done by the store (weigh a fish before and after each preparation,
  several times), not copied from the guidance values. Wrong yields mean wrong stock reservations.
- Pieces and packs are counted, so their yield is always 100 %.

## 10. Allergens and labelling

- Every product: fish, crustaceans or molluscs as applicable.
- Marinated and ready-to-cook products: full ingredient list and allergens (for example *Contains: crustaceans,
  mustard*), net weight, and any further FSSAI labelling the packs need [Legal review].

## 11. Images

- Real photographs of the product as delivered (after the default preparation), on a plain light background, natural
  light; no stock photos and no photos of a different species. Sample placeholders are listed in
  `backend/data/sample/images.json` and are replaced by the owner's photography.
- At least 800 px on the shortest side; alt text describes the picture (*Seer fish steaks on a white plate*).

## 12. HSN code and tax category

HSN codes and tax categories are chosen with the CA. The sample file uses 4-digit placeholders (0302, 0304, 0306,
0307) marked "confirm with CA"; do not copy them to real products without that confirmation.
