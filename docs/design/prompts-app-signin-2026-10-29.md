# Prompts: app sign-in avatar (2026-10-29)

The app's sign-in screen follows the owner's reference `design/mockups/app-login-reference-2026-10-29.jpg` (D281).
Its avatar is Ammamma, our `HOME_COOK` character (D282), in the existing slot `LOGIN_OTP`. The prompt below is the
slot's `prompt` in `packages/illustrations/manifest.json`, built from the scene, the character sheet, the style block,
the background and the shape (`illustrations.md` §2), and it ends with the no-text line. Paste it unchanged into an
image tool whose licence allows commercial use.

The coastal scene in the header is the owner's harbour picture, slot `SIGN_IN_COAST` (D286); its prompt is below.
The drawn `FreshCoastScene` in `fresh_theme` is only its fallback.

## How to deliver

| Slot | Save as (`docs/design/characters/generated/`) | Size | Notes | Delivered |
|---|---|---|---|---|
| `LOGIN_OTP` | `login_otp.png` (transparent PNG) | 1024 × 1024 | Head and shoulders fill the middle of the square; the app shows it inside a 112–156 px circle on a pale mint backdrop, so keep the bun and the smile away from the corners. The website login and the app's signed-out account and orders screens use the same picture. | **Yes, 2026-10-29** (1254 × 1254 original; FINAL, D285) |
| `SIGN_IN_COAST` | `sign_in_coast.png` (transparent PNG) | 1536 × 1024 | The harbour on the right of the coral header; the top-left fades to transparent so the title can sit there, and the water at the bottom runs under the card. | **Yes, 2026-10-29** (FINAL, D286) |

Both pictures are bundled: the app shows them, and draws its own chest-up Ammamma (`AmmammaPortrait`) and coast
(`FreshCoastScene`) only if a file is missing.

## LOGIN_OTP (sign-in avatar)

```text
A friendly chest-up portrait of the home cook for a round profile avatar: she faces the viewer with a warm closed-mouth smile, her silver hair in a neat bun on top of her head, a small red bindi, her mango-yellow saree with a coral sash across one shoulder; her head and shoulders fill the middle of the square with a little space around the bun, no hands, no phone and no props. Ammamma, the home cook: A grandmother, about 65, with silver hair in a bun, a red bindi and a warm knowing smile; a mango-yellow saree with a coral border over a coral blouse, gold bangles and a thin gold necklace; she carries a steel pot and a ladle; same face, hair and clothes as in the approved character sheet in every picture. Highly detailed, realistic 3D animated-film style character render, soft subsurface skin, natural hair strands and fabric texture, expressive friendly faces, warm cinematic studio lighting with soft shadows, shallow depth of field, clean modern look, warm sand-coloured background (#FBF4E8) unless stated, brand colours sea teal (#0B7D70) and coral (#F07A5A) as accents, set in coastal Andhra Pradesh, India. Background: transparent (or, if transparency is not available, a plain flat white background), the subject isolated with nothing around it so it can be cut out cleanly. Square 1:1 image, subject centred, full body or three-quarter view. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

If the tool adds a steel pot or a ladle from the character sheet, ask again with "no props in her hands".

## SIGN_IN_COAST (sign-in header harbour)

```text
A small coastal harbour at sunrise for the right side of a sign-in header: two wooden fishing trawlers painted orange and teal moored side by side at a stone jetty with rope lines, rounded rocks on the left, three coconut palms behind the jetty, a large pale sun behind soft peach clouds, two white gulls in the sky and calm teal water with sun reflections across the bottom of the picture; the top-left corner fades to empty so text can sit there; no people, no characters and no faces. Highly detailed, realistic 3D animated-film style character render, soft subsurface skin, natural hair strands and fabric texture, expressive friendly faces, warm cinematic studio lighting with soft shadows, shallow depth of field, clean modern look, warm sand-coloured background (#FBF4E8) unless stated, brand colours sea teal (#0B7D70) and coral (#F07A5A) as accents, set in coastal Andhra Pradesh, India. Background: transparent (or, if transparency is not available, a plain flat white background), the subject isolated with nothing around it so it can be cut out cleanly. Wide 3:2 image, the subject on the right half with the left third kept empty. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

If the tool draws people on the jetty, ask again with "no people anywhere".
