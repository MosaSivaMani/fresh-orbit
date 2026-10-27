#!/usr/bin/env python3
"""Make the sample set's picture files from the owner's pictures in docs/design/characters/generated.

Writes small WebP copies (transparency kept) to backend/data/sample/images/, at the path of their image key without
the leading "sample/" (sample/categories/fish.webp -> images/categories/fish.webp; docs/product/sample-data.md).
Runs on the CI runner (workflow sample-images.yml), never on the development Mac; the result is committed.

Usage: python3 backend/data/sample/tools/make_images.py [repo_root]   (needs Pillow)
"""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image

ROOT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[4]
SOURCE = ROOT / "docs" / "design" / "characters" / "generated"
TARGET = ROOT / "backend" / "data" / "sample" / "images"

# Owner picture -> (path under images/, longest side in pixels).
PICTURES = {
    "category_fish.png": ("categories/fish.webp", 512),
    "category_prawns.png": ("categories/prawns-shrimp.webp", 512),
    "category_crabs.png": ("categories/crabs.webp", 512),
    "category_shellfish.png": ("categories/shellfish.webp", 512),
    "sample_banner_seer_steaks.png": ("banners/welcome.webp", 1200),
    "sample_banner_prawns.png": ("banners/prawn-week.webp", 1200),
}


def main() -> int:
    for source_name, (target_name, longest) in PICTURES.items():
        with Image.open(SOURCE / source_name) as image:
            picture = image.convert("RGBA")
            picture.thumbnail((longest, longest), Image.Resampling.LANCZOS)
            target = TARGET / target_name
            target.parent.mkdir(parents=True, exist_ok=True)
            picture.save(target, "WEBP", quality=82, method=6)
            print(f"{target.relative_to(ROOT)} {picture.width}x{picture.height} {target.stat().st_size} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
