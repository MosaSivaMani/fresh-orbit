# Prompts: app sign-in avatar (2026-10-29)

The app's sign-in screen follows the owner's reference `design/mockups/app-login-reference-2026-10-29.jpg` (D281).
Its avatar is Ammamma, our `HOME_COOK` character (D282), in the existing slot `LOGIN_OTP`. The prompt below is the
slot's `prompt` in `packages/illustrations/manifest.json`, built from the scene, the character sheet, the style block,
the background and the shape (`illustrations.md` §2), and it ends with the no-text line. Paste it unchanged into an
image tool whose licence allows commercial use.

The coastal scene in the header is drawn in code (`FreshCoastScene` in `fresh_theme`), so it needs no picture.

## How to deliver

| Slot | Save as (`docs/design/characters/generated/`) | Size | Notes |
|---|---|---|---|
| `LOGIN_OTP` | `login_otp.png` (transparent PNG) | 1024 × 1024 | Head and shoulders fill the middle of the square; the app shows it inside a 112–156 px circle on a pale mint backdrop, so keep the bun and the smile away from the corners. The website login and the app's signed-out account and orders screens use the same picture. |

Until the PNG is bundled the app draws its own chest-up Ammamma (`AmmammaPortrait`) in the ring.

## LOGIN_OTP (sign-in avatar)

```text
A friendly chest-up portrait of the home cook for a round profile avatar: she faces the viewer with a warm closed-mouth smile, her silver hair in a neat bun on top of her head, a small red bindi, her mango-yellow saree with a coral sash across one shoulder; her head and shoulders fill the middle of the square with a little space around the bun, no hands, no phone and no props. Ammamma, the home cook: A grandmother, about 65, with silver hair in a bun, a red bindi and a warm knowing smile; a mango-yellow saree with a coral border over a coral blouse, gold bangles and a thin gold necklace; she carries a steel pot and a ladle; same face, hair and clothes as in the approved character sheet in every picture. Highly detailed, realistic 3D animated-film style character render, soft subsurface skin, natural hair strands and fabric texture, expressive friendly faces, warm cinematic studio lighting with soft shadows, shallow depth of field, clean modern look, warm sand-coloured background (#FBF4E8) unless stated, brand colours sea teal (#0B7D70) and coral (#F07A5A) as accents, set in coastal Andhra Pradesh, India. Background: transparent (or, if transparency is not available, a plain flat white background), the subject isolated with nothing around it so it can be cut out cleanly. Square 1:1 image, subject centred, full body or three-quarter view. No text, no letters, no numbers, no logos, no watermark. High resolution.
```

If the tool adds a steel pot or a ladle from the character sheet, ask again with "no props in her hands".
