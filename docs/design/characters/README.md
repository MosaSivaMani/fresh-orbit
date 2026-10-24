# Character sheet (approved 2026-10-24)

`character-sheet.jpg` is the owner-approved reference for the five Fresh Orbit characters (D53). The owner generated it in ChatGPT from the prompt pack. Every later image must match it.

| Character | Look |
|---|---|
| Padma, fishmonger | Coral headscarf over a low bun, white kurta, glossy sea-teal apron, light-blue gloves, dark-green rubber boots, holds a silver fish |
| Ravi, rider | Sea-teal helmet with a clear visor, teal rain jacket with reflective stripes, ice-blue insulated backpack with a white snowflake, dark cargo trousers, white sneakers, short beard |
| Anil, support | Round thin glasses, wavy black hair, teal polo, slim headset, navy trousers |
| Ammamma, home cook | Silver hair in a bun, red bindi, mango-yellow saree with a coral border, gold bangles, steel pot and ladle |
| Chanduva, pomfret | Round silver pomfret, big glossy eyes, rosy cheeks, open happy smile, fins used as hands |

When generating more images, say "Use these exact characters" in the same chat as the sheet, or attach this image.

## Generated images received
Owner-generated finals land in `generated/` as `<slot_lower_snake>.jpg` (opaque background) or `.png` (transparent). The illustrations package picks them up from here.

| Slot | File | Received |
|---|---|---|
| ONBOARDING_FRESH | `generated/onboarding_fresh.jpg` | 2026-10-24 |
| ONBOARDING_CLEANED | `generated/onboarding_cleaned.jpg` | 2026-10-24 |
| ONBOARDING_DELIVERY | `generated/onboarding_delivery.jpg` | 2026-10-24 |
| LOCATION_PICK | `generated/location_pick.jpg` | 2026-10-24 |
| NOT_SERVICEABLE | `generated/not_serviceable.jpg` | 2026-10-24 |
| EMPTY_CART | `generated/empty_cart.jpg` | 2026-10-24 |
| EMPTY_WISHLIST | `generated/empty_wishlist.jpg` | 2026-10-24 |
| EMPTY_ORDERS | `generated/empty_orders.jpg` | 2026-10-24 |
| EMPTY_NOTIFICATIONS | `generated/empty_notifications.jpg` | 2026-10-24 |
| EMPTY_ADDRESSES | `generated/empty_addresses.jpg` (Ravi with map and pin) | 2026-10-24 |
| ONBOARDING_TILE_MORNING_CATCH | `generated/onboarding_tile_morning_catch.png` (transparent) | 2026-10-24 |
| ONBOARDING_TILE_KEPT_CHILLED | `generated/onboarding_tile_kept_chilled.png` (transparent) | 2026-10-24 |
| ONBOARDING_TILE_NET_WEIGHT | `generated/onboarding_tile_net_weight.png` (transparent) | 2026-10-24 |
| ONBOARDING_TILE_COLD_BOX | `generated/onboarding_tile_cold_box.png` (transparent) | 2026-10-24 |
| ONBOARDING_BANNER_CLEANED | `generated/onboarding_banner_cleaned.png` (transparent) | 2026-10-24 |

The five onboarding PNGs are 1.4–2.2 MB, over the package's 600 KB limit, so they are not imported into
`packages/illustrations` yet (their slots stay PLACEHOLDER there): run `import` and then `optimize` (sharp makes
a WebP of at most 600 KB) on a machine where that is allowed. The mobile app already bundles copies resized with
`sips` (tiles 512 px, banner 1024 × 512, transparency kept) in `apps/mobile/assets/illustrations/`.
