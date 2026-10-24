#!/usr/bin/env python3
"""Validate the sample data set in backend/data/sample.

Checks that every file parses, every natural-key reference resolves (also against backend/data/reference), and that
values follow the rules the backend will enforce when the loaders replay them (unit rules CAT-R4, yields CAT-R6,
quantity rules CAT-R7, price range PRC-R5, explicit preparation charges PRC-R8, tax rule present PRC-R9, fee rules
LOC-R5, slot template rules DEL-R1/R2, coupon rules PROMO-R19/R20, address rules CUS-R5, city readiness LOC-R6).

Usage: python3 backend/data/sample/tools/validate.py [sample_dir]
Exit code 0 when valid, 1 with one line per problem otherwise. Standard library only.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

SAMPLE_DIR = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent
REFERENCE_DIR = SAMPLE_DIR.parent / "reference"

UPPER_SNAKE = re.compile(r"^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$")
LOCATION_CODE = re.compile(r"^[A-Z0-9]+([_-][A-Z0-9]+)*$")
SKU = re.compile(r"^[A-Z0-9]+([_-][A-Z0-9]+)*$")
SLUG = re.compile(r"^[a-z0-9]+(-[a-z0-9]+)*$")
PINCODE = re.compile(r"^[1-9][0-9]{5}$")
E164 = re.compile(r"^\+[1-9][0-9]{7,14}$")
# Fake block reserved in docs/product/sample-data.md (S-D3): +91 99999 00NNN.
TEST_PHONE = re.compile(r"^\+919999900[0-9]{3}$")
GSTIN_ALPHABET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"
GSTIN = re.compile(r"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$")


def gstin_check_character(first14: str) -> str:
    """Same Luhn mod 36 as Settings.Domain Gstins.CheckCharacter."""
    total = 0
    for i, ch in enumerate(first14):
        product = GSTIN_ALPHABET.index(ch) * ((i % 2) + 1)
        total += product // 36 + product % 36
    return GSTIN_ALPHABET[(36 - total % 36) % 36]


def gstin_valid(value: str) -> bool:
    if not GSTIN.match(value or ""):
        return False
    state = int(value[:2])
    if not (1 <= state <= 38 or state in (97, 99)):
        return False
    return gstin_check_character(value[:14]) == value[14]
STATE = re.compile(r"^[A-Z]{2}$")
HSN = re.compile(r"^[0-9]{4,8}$")
COUPON_CODE = re.compile(r"^[A-Z0-9]{3,20}$")
HHMM = re.compile(r"^([01][0-9]|2[0-3]):([0-5][0-9])$")
EMAIL = re.compile(r"^[^@\s]+@[^@\s]+\.[a-z]{2,}$")
RESERVED_EMAIL_DOMAINS = (".example", ".invalid", ".test")
DAYS = ["MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY"]
PRICE_MIN, PRICE_MAX = 1, 5_000_000

errors: list[str] = []


def err(where: str, message: str) -> None:
    errors.append(f"{where}: {message}")


def load(path: Path) -> dict:
    try:
        with path.open(encoding="utf-8") as f:
            return json.load(f)
    except (OSError, json.JSONDecodeError) as e:
        err(str(path), f"cannot be parsed ({e})")
        return {}


def minutes(hhmm: str) -> int:
    h, m = hhmm.split(":")
    return int(h) * 60 + int(m)


def require_sample_note(where: str, record: dict) -> None:
    note = record.get("sampleNote")
    if not isinstance(note, str) or not note.startswith("[SAMPLE]"):
        err(where, "sampleNote starting with '[SAMPLE]' is required")


def no_marker(where: str, field: str, value) -> None:
    if isinstance(value, str) and "[SAMPLE]" in value:
        err(where, f"{field} is customer-visible and must not contain '[SAMPLE]' (use sampleNote)")


def unique(where: str, values: list, what: str) -> set:
    seen, dupes = set(), set()
    for v in values:
        (dupes if v in seen else seen).add(v)
    for d in sorted(dupes, key=str):
        err(where, f"duplicate {what} '{d}'")
    return seen


# ------------------------------------------------------------------ reference data
prep_codes = {i["code"] for i in load(REFERENCE_DIR / "catalog/preparation-methods.json").get("items", [])}
tax_categories = {i["code"] for i in load(REFERENCE_DIR / "pricing/tax-categories.json").get("items", [])}
role_codes = {r["code"] for r in load(REFERENCE_DIR / "identity/roles.json").get("roles", [])}
storage_units = {u["code"] for u in load(REFERENCE_DIR / "inventory/storage-units.json").get("storageUnits", [])}
for name, values in [("preparation methods", prep_codes), ("tax categories", tax_categories), ("roles", role_codes),
                     ("storage units", storage_units)]:
    if not values:
        err("reference", f"no {name} found under {REFERENCE_DIR}")

# ------------------------------------------------------------------ files
files = {}
for path in sorted(SAMPLE_DIR.glob("*.json")):
    files[path.name] = load(path)

manifest = files.get("sample-set.json", {})
for step in manifest.get("loadOrder", []):
    if step.get("file") not in files:
        err("sample-set.json", f"loadOrder names missing file '{step.get('file')}'")
orders = [s.get("order") for s in manifest.get("loadOrder", [])]
if orders != sorted(orders):
    err("sample-set.json", "loadOrder must be in ascending order")

# ------------------------------------------------------------------ locations
loc = files.get("locations.json", {})
cities = {c.get("code"): c for c in loc.get("cities", [])}
unique("locations.cities", [c.get("code") for c in loc.get("cities", [])], "city code")
for code, c in cities.items():
    w = f"locations.cities[{code}]"
    require_sample_note(w, c)
    if not (isinstance(code, str) and UPPER_SNAKE.match(code) and len(code) <= 40):
        err(w, "code must be UPPER_SNAKE, at most 40 characters")
    if not STATE.match(c.get("stateCode") or ""):
        err(w, "stateCode must be two upper-case letters")
    if not c.get("timeZone"):
        err(w, "timeZone is required (IANA name)")

locations = {l.get("code"): l for l in loc.get("fulfillmentLocations", [])}
unique("locations.fulfillmentLocations", [l.get("code") for l in loc.get("fulfillmentLocations", [])], "location code")
for code, l in locations.items():
    w = f"locations.fulfillmentLocations[{code}]"
    require_sample_note(w, l)
    if not (isinstance(code, str) and LOCATION_CODE.match(code)):
        err(w, "code must be upper-case letters/digits joined by '_' or '-'")
    if l.get("cityCode") not in cities:
        err(w, f"cityCode '{l.get('cityCode')}' does not exist")
    if not PINCODE.match(l.get("pincode") or ""):
        err(w, "pincode must be 6 digits not starting with 0")
    if l.get("targetStatus") == "ACTIVE":  # LOC-R11
        for field in ("addressLine", "pincode", "latitude", "longitude", "fssaiLicenseNo"):
            if l.get(field) in (None, ""):
                err(w, f"{field} is required for an ACTIVE location (LOC-R11)")
        if l.get("hasColdStorage") is not True:
            err(w, "hasColdStorage must be true for an ACTIVE location (LOC-R11)")
    if l.get("phone") and not TEST_PHONE.match(l["phone"]):
        err(w, "phone must use the fake block +919999900NNN (S-D3)")

hours_by_location = {}
for h in loc.get("operatingHours", []):
    w = f"locations.operatingHours[{h.get('locationCode')}]"
    require_sample_note(w, h)
    if h.get("locationCode") not in locations:
        err(w, "locationCode does not exist")
    days = [d.get("dayOfWeek") for d in h.get("days", [])]
    unique(w, days, "dayOfWeek")
    for d in h.get("days", []):
        if d.get("dayOfWeek") not in DAYS:
            err(w, f"dayOfWeek '{d.get('dayOfWeek')}' must be one of {DAYS}")
        o, c = d.get("opensAt") or "", d.get("closesAt") or ""
        if not (HHMM.match(o) and HHMM.match(c)) or minutes(o) >= minutes(c):
            err(w, f"{d.get('dayOfWeek')}: opensAt must be before closesAt, both HH:mm (LOC-R13)")
    hours_by_location[h.get("locationCode")] = days

for cl in loc.get("closures", []):
    w = f"locations.closures[{cl.get('locationCode')}]"
    require_sample_note(w, cl)
    if cl.get("locationCode") not in locations:
        err(w, "locationCode does not exist")
    if not isinstance(cl.get("closedOnOffsetDays"), int) or cl["closedOnOffsetDays"] < 1:
        err(w, "closedOnOffsetDays must be an integer >= 1 so the closure is in the future (LOC-R14)")

service_areas = {a.get("key"): a for a in loc.get("serviceAreas", [])}
unique("locations.serviceAreas", [a.get("key") for a in loc.get("serviceAreas", [])], "service-area key")
all_pincodes = []
for key, a in service_areas.items():
    w = f"locations.serviceAreas[{key}]"
    require_sample_note(w, a)
    if a.get("cityCode") not in cities:
        err(w, "cityCode does not exist")
    l = locations.get(a.get("locationCode"))
    if l is None:
        err(w, "locationCode does not exist")
    elif l.get("cityCode") != a.get("cityCode"):
        err(w, "location belongs to another city (LOC-R4)")
    fee, free, mn = a.get("deliveryFeePaise"), a.get("freeDeliveryAbovePaise"), a.get("minOrderPaise")
    if not isinstance(fee, int) or fee < 0 or not isinstance(mn, int) or mn < 0:
        err(w, "deliveryFeePaise and minOrderPaise must be integers >= 0 (LOC-R5)")
    elif free is not None and (not isinstance(free, int) or free < mn):
        err(w, "freeDeliveryAbovePaise must be null or >= minOrderPaise (LOC-R5)")
    pins = a.get("pincodes", [])
    if not pins:
        err(w, "at least one pincode is required (LOC-R6)")
    for pin in pins:
        if not PINCODE.match(pin or ""):
            err(w, f"pincode '{pin}' must be 6 digits not starting with 0 (LOC-R2)")
    all_pincodes += pins
unique("locations.serviceAreas", all_pincodes, "pincode (LOC-R3: active in one area only)")
served_pincodes = set(all_pincodes)

# ------------------------------------------------------------------ catalog
cat = files.get("catalog.json", {})
categories = {c.get("slug"): c for c in cat.get("categories", [])}
unique("catalog.categories", [c.get("slug") for c in cat.get("categories", [])], "category slug")
for slug, c in categories.items():
    w = f"catalog.categories[{slug}]"
    require_sample_note(w, c)
    no_marker(w, "name", c.get("name"))
    if not (isinstance(slug, str) and SLUG.match(slug) and len(slug) <= 80):
        err(w, "slug must be lowercase kebab, at most 80 characters (CAT-R1)")
    parent = c.get("parentSlug")
    if parent is not None:
        if parent not in categories:
            err(w, f"parentSlug '{parent}' does not exist")
        elif categories[parent].get("parentSlug") is not None:
            err(w, "category depth must be at most 2 (CAT-R2)")

products = {p.get("slug"): p for p in cat.get("products", [])}
unique("catalog.products", [p.get("slug") for p in cat.get("products", [])], "product slug")
variants = {}  # sku -> (product, variant)
all_skus = []
for slug, p in products.items():
    w = f"catalog.products[{slug}]"
    require_sample_note(w, p)
    for field in ("name", "description", "storageInstructions", "handlingNotes"):
        no_marker(w, field, p.get(field))
    if not (isinstance(slug, str) and SLUG.match(slug) and len(slug) <= 80):
        err(w, "slug must be lowercase kebab, at most 80 characters (CAT-R1)")
    if p.get("categorySlug") not in categories:
        err(w, f"categorySlug '{p.get('categorySlug')}' does not exist")
    if not p.get("name") or len(p["name"]) > 120:
        err(w, "name must be 1 to 120 characters")
    names = p.get("localNames", [])
    if len(names) > 10 or any(len(n) > 40 for n in names):
        err(w, "localNames: at most 10, each at most 40 characters (CAT-R19)")
    unique(w, [n.lower() for n in names], "local name (case-insensitive)")
    if set(p.get("localNameLanguages", {}).keys()) != set(names):
        err(w, "localNameLanguages keys must match localNames")
    if p.get("sourceType") not in ("WILD", "FARMED"):
        err(w, "sourceType must be WILD or FARMED")
    if not HSN.match(p.get("hsnCode") or ""):
        err(w, "hsnCode must be 4 to 8 digits (CAT-R3)")
    if "confirm with CA" not in (p.get("hsnNote") or ""):
        err(w, "hsnNote must say the HSN code is a placeholder to confirm with CA")
    if p.get("taxCategoryCode") not in tax_categories:
        err(w, f"taxCategoryCode '{p.get('taxCategoryCode')}' is not in reference tax categories")
    for field in ("description", "storageInstructions"):
        if len(p.get(field) or "") > 2000:
            err(w, f"{field} must be at most 2000 characters")
    active_variants = [v for v in p.get("variants", []) if v.get("isActive")]
    if p.get("targetStatus") == "ACTIVE" and not active_variants:
        err(w, "an ACTIVE product needs at least one active variant (CAT-R3)")
    for v in p.get("variants", []):
        sku = v.get("sku")
        vw = f"{w}.variants[{sku}]"
        all_skus.append(sku)
        variants[sku] = (p, v)
        no_marker(vw, "name", v.get("name"))
        if not (isinstance(sku, str) and SKU.match(sku) and len(sku) <= 40):
            err(vw, "sku must be upper-case letters/digits joined by '_' or '-', at most 40 characters (CAT-R8)")
        unit, stock, wv = v.get("sellUnit"), v.get("stockUnit"), v.get("isWeightVariable")
        # CAT-R4 unit rules (D10: PACK is stocked in PIECE)
        if unit == "KG":
            if stock != "GRAM" or wv is not True:
                err(vw, "KG variants must have stockUnit GRAM and isWeightVariable true (CAT-R4)")
            if v.get("packSizeGrams") is not None or v.get("avgPieceGrams") is not None:
                err(vw, "KG variants take neither packSizeGrams nor avgPieceGrams")
        elif unit == "PIECE":
            if stock != "PIECE" or wv is not False:
                err(vw, "PIECE variants must have stockUnit PIECE and isWeightVariable false (CAT-R4)")
            if not isinstance(v.get("avgPieceGrams"), int) or v["avgPieceGrams"] <= 0:
                err(vw, "PIECE variants need avgPieceGrams > 0 (CAT-R4)")
        elif unit == "PACK":
            if stock != "PIECE" or wv is not False:
                err(vw, "PACK variants must have stockUnit PIECE and isWeightVariable false (CAT-R4, D10)")
            if not isinstance(v.get("packSizeGrams"), int) or v["packSizeGrams"] <= 0:
                err(vw, "PACK variants need packSizeGrams > 0 (CAT-R4)")
        else:
            err(vw, "sellUnit must be KG, PIECE or PACK")
        # CAT-R7 quantity rules
        mn, mx, st = v.get("minOrder"), v.get("maxOrder"), v.get("orderStep")
        if not all(isinstance(x, int) for x in (mn, mx, st)) or st <= 0 or mn < st or mn % st or mx < mn or mx % st:
            err(vw, "quantity rules: orderStep > 0, minOrder >= step and a multiple of it, maxOrder >= minOrder and a multiple of step (CAT-R7)")
        # CAT-R5 / CAT-R6 preparations
        preps = v.get("preparations", [])
        codes = [x.get("code") for x in preps]
        unique(vw, codes, "preparation code")
        if v.get("isActive") and sum(1 for x in preps if x.get("isDefault")) != 1:
            err(vw, "an active variant needs exactly one default preparation (CAT-R5)")
        for x in preps:
            code, y = x.get("code"), x.get("yieldBp")
            if code not in prep_codes:
                err(vw, f"preparation '{code}' is not in reference preparation methods")
            if not isinstance(y, int) or not 1 <= y <= 10000:
                err(vw, f"{code}: yieldBp must be 1..10000 (CAT-R6)")
            elif code == "WHOLE" and y != 10000:
                err(vw, "WHOLE must have yieldBp 10000 (CAT-R6)")
            elif unit in ("PIECE", "PACK") and y != 10000:
                err(vw, f"{code}: PIECE/PACK variants must use yieldBp 10000 (CAT-R6)")
unique("catalog.products", all_skus, "sku (CAT-R8)")

# ------------------------------------------------------------------ pricing
pr = files.get("pricing.json", {})
tax_rules = {}
for t in pr.get("taxRules", []):
    w = f"pricing.taxRules[{t.get('taxCategoryCode')}]"
    require_sample_note(w, t)
    if t.get("taxCategoryCode") not in tax_categories:
        err(w, "taxCategoryCode is not in reference tax categories")
    if not isinstance(t.get("rateBp"), int) or not 0 <= t["rateBp"] <= 2800:
        err(w, "rateBp must be 0..2800 (PRC-R9)")
    if t.get("rateIsPlaceholder") is not True or "confirm with CA" not in (t.get("sampleNote") or ""):
        err(w, "sample tax rates must be marked rateIsPlaceholder true and 'confirm with CA'")
    tax_rules[t.get("taxCategoryCode")] = t
unique("pricing.taxRules", [t.get("taxCategoryCode") for t in pr.get("taxRules", [])], "tax category (one period each)")
for slug, p in products.items():
    if p.get("taxCategoryCode") not in tax_rules:
        err(f"catalog.products[{slug}]", f"no sample tax rule for '{p.get('taxCategoryCode')}' (PRC-R9: not for sale)")
if "DELIVERY_SERVICE" not in tax_rules:
    err("pricing.taxRules", "DELIVERY_SERVICE needs a rule because the service areas charge a delivery fee (PRC-R10)")

bulk = pr.get("bulk", {})
require_sample_note("pricing.bulk", bulk)
rows = bulk.get("rows", [])
if len(rows) > 5000:
    err("pricing.bulk", "at most 5000 rows (PRC-R13)")
priced, charged = {}, {}
for i, r in enumerate(rows):
    w = f"pricing.bulk.rows[{i}]"
    if r.get("locationCode") not in locations:
        err(w, f"locationCode '{r.get('locationCode')}' does not exist")
    if r.get("sku") not in variants:
        err(w, f"sku '{r.get('sku')}' does not exist")
        continue
    _, v = variants[r["sku"]]
    if r.get("kind") == "PRICE":
        key = (r["locationCode"], r["sku"])
        if key in priced:
            err(w, f"duplicate PRICE row for {key} (PRC-R13)")
        priced[key] = r
        up, cmp_ = r.get("unitPricePaise"), r.get("compareAtPaise")
        if not isinstance(up, int) or not PRICE_MIN <= up <= PRICE_MAX:
            err(w, f"unitPricePaise must be {PRICE_MIN}..{PRICE_MAX} (PRC-R5)")
        elif cmp_ is not None and (not isinstance(cmp_, int) or cmp_ <= up or cmp_ > PRICE_MAX):
            err(w, "compareAtPaise must be null or greater than unitPricePaise (PRC-R5)")
    elif r.get("kind") == "PREP_CHARGE":
        code = r.get("preparationCode")
        key = (r["locationCode"], r["sku"], code)
        if key in charged:
            err(w, f"duplicate PREP_CHARGE row for {key} (PRC-R13)")
        charged[key] = r
        if code not in {x["code"] for x in v.get("preparations", [])}:
            err(w, f"preparation '{code}' is not enabled on {r['sku']} (PRC-R7)")
        ch, basis = r.get("chargePaise"), r.get("chargeBasis")
        if not isinstance(ch, int) or not 0 <= ch <= PRICE_MAX:
            err(w, f"chargePaise must be 0..{PRICE_MAX}")
        allowed = {"KG": {"PER_KG", "FLAT"}, "PIECE": {"PER_ITEM", "FLAT"}, "PACK": {"PER_ITEM", "FLAT"}}.get(v.get("sellUnit"), set())
        if basis not in allowed:
            err(w, f"chargeBasis '{basis}' is not valid for sell unit {v.get('sellUnit')} (PRC-R7)")
    else:
        err(w, "kind must be PRICE or PREP_CHARGE")
for lcode in locations:
    for sku, (p, v) in variants.items():
        if not v.get("isActive"):
            continue
        if (lcode, sku) not in priced:
            err(f"pricing[{lcode}]", f"active variant {sku} has no price")
        for x in v.get("preparations", []):
            if (lcode, sku, x["code"]) not in charged:
                err(f"pricing[{lcode}]", f"{sku} preparation {x['code']} has no explicit charge row (PRC-R8)")

# ------------------------------------------------------------------ inventory
inv = files.get("inventory.json", {})
stock_items = {}
for s in inv.get("stockItems", []):
    key = (s.get("locationCode"), s.get("sku"))
    w = f"inventory.stockItems[{key[0]}/{key[1]}]"
    require_sample_note(w, s)
    if key in stock_items:
        err(w, "duplicate stock item (unique location x variant)")
    stock_items[key] = s
    if key[0] not in locations:
        err(w, "locationCode does not exist")
    if key[1] not in variants:
        err(w, "sku does not exist")
    if not isinstance(s.get("lowStockThreshold"), int) or s["lowStockThreshold"] < 0:
        err(w, "lowStockThreshold must be an integer >= 0")
for lcode in locations:
    for sku, (_, v) in variants.items():
        if v.get("isActive") and (lcode, sku) not in stock_items:
            err(f"inventory[{lcode}]", f"active variant {sku} has no stock item (not visible, CAT-R10)")

batch_codes = []
for r in inv.get("receipts", []):
    key = (r.get("locationCode"), r.get("sku"))
    w = f"inventory.receipts[{r.get('batchCode')}]"
    require_sample_note(w, r)
    batch_codes.append((r.get("locationCode"), r.get("batchCode")))
    if key not in stock_items:
        err(w, "no stock item for this location and sku")
    if not r.get("batchCode") or len(r["batchCode"]) > 40:
        err(w, "batchCode is required, at most 40 characters")
    q = r.get("receivedQuantity") or {}
    if key[1] in variants:
        expected = variants[key[1]][1].get("stockUnit")
        if q.get("unit") != expected:
            err(w, f"receivedQuantity.unit must be the stock unit {expected}")
    if not isinstance(q.get("value"), int) or q["value"] <= 0:
        err(w, "receivedQuantity.value must be an integer > 0")
    if not isinstance(r.get("totalCostPaise"), int) or r["totalCostPaise"] <= 0:
        err(w, "totalCostPaise must be an integer > 0")
    recv, bb, caught = r.get("receivedAtOffsetHours"), r.get("bestBeforeOffsetHours"), r.get("caughtOrHarvestedOnOffsetDays")
    if not all(isinstance(x, int) for x in (recv, bb, caught)):
        err(w, "receivedAtOffsetHours, bestBeforeOffsetHours and caughtOrHarvestedOnOffsetDays must be integers")
    else:
        if recv > 0:
            err(w, "receivedAtOffsetHours must be <= 0 (received before load)")
        if bb <= 0:
            err(w, "bestBeforeOffsetHours must be > 0 so the batch is sellable at load")
        if caught > 0 or caught * 24 > recv + 24:
            err(w, "caughtOrHarvestedOnOffsetDays must be on or before the receipt day")
    t = r.get("receiptTemperatureC")
    if not isinstance(t, (int, float)) or not -40.0 <= t <= 40.0:
        err(w, "receiptTemperatureC must be -40.0..40.0")
    elif t > 4.0 and "cold-chain warning" not in (r.get("sampleNote") or ""):
        err(w, "a receipt above 4.0 °C must say in sampleNote that it demonstrates the cold-chain warning")
    # cost should be below the selling value of the same quantity (sanity check on sample margins)
    if key[1] in variants and (key[0], key[1]) in priced and isinstance(q.get("value"), int):
        v = variants[key[1]][1]
        up = priced[(key[0], key[1])]["unitPricePaise"]
        sell_value = q["value"] * up // 1000 if v.get("stockUnit") == "GRAM" else q["value"] * up
        if r["totalCostPaise"] >= sell_value:
            err(w, "totalCostPaise should be below the selling value of the received quantity")
unique("inventory.receipts", batch_codes, "batch code per location")
for c in inv.get("coldChainLogs", []):
    w = f"inventory.coldChainLogs[{c.get('storageUnit')}]"
    require_sample_note(w, c)
    if c.get("locationCode") not in locations:
        err(w, "locationCode does not exist")
    if c.get("storageUnit") not in storage_units:
        err(w, "storageUnit is not in reference storage units")

# ------------------------------------------------------------------ delivery
dl = files.get("delivery.json", {})
templates_by_day = {}
for i, t in enumerate(dl.get("slotTemplates", [])):
    w = f"delivery.slotTemplates[{i}]"
    require_sample_note(w, t)
    if t.get("locationCode") not in locations:
        err(w, "locationCode does not exist")
    d, s, e = t.get("dayOfWeek"), t.get("startTime") or "", t.get("endTime") or ""
    if not isinstance(d, int) or not 1 <= d <= 7:
        err(w, "dayOfWeek must be 1 (Monday) .. 7 (Sunday)")
    if not (HHMM.match(s) and HHMM.match(e)) or minutes(s) % 15 or minutes(e) % 15 or minutes(s) >= minutes(e):
        err(w, "startTime < endTime, HH:mm on 15-minute boundaries (DEL-R1)")
        continue
    if not isinstance(t.get("capacity"), int) or not 1 <= t["capacity"] <= 500:
        err(w, "capacity must be 1..500 (DEL-R1)")
    if not isinstance(t.get("cutoffMinutes"), int) or not 0 <= t["cutoffMinutes"] <= 2880:
        err(w, "cutoffMinutes must be 0..2880 (DEL-R1)")
    templates_by_day.setdefault((t.get("locationCode"), d), []).append((minutes(s), minutes(e), i))
for (lcode, d), spans in templates_by_day.items():
    spans.sort()
    for (s1, e1, i1), (s2, e2, i2) in zip(spans, spans[1:]):
        if s2 < e1:
            err(f"delivery.slotTemplates[{i2}]", f"overlaps template {i1} on day {d} (DEL-R2)")
for lcode in locations:
    if not any(k[0] == lcode for k in templates_by_day):
        err(f"delivery[{lcode}]", "no slot template (LOC-R6 SLOT_TEMPLATE_MISSING)")

riders = {r.get("key"): r for r in dl.get("riders", [])}
unique("delivery.riders", [r.get("key") for r in dl.get("riders", [])], "rider key")
unique("delivery.riders", [r.get("phone") for r in dl.get("riders", [])], "rider phone")
for key, r in riders.items():
    w = f"delivery.riders[{key}]"
    require_sample_note(w, r)
    no_marker(w, "fullName", r.get("fullName"))
    if r.get("locationCode") not in locations:
        err(w, "locationCode does not exist")
    if not r.get("fullName") or len(r["fullName"]) > 120:
        err(w, "fullName must be 1 to 120 characters")
    if not E164.match(r.get("phone") or "") or not TEST_PHONE.match(r.get("phone") or ""):
        err(w, "phone must be E.164 in the fake block +919999900NNN (S-D3)")

# ------------------------------------------------------------------ promotions
pm = files.get("promotions.json", {})
coupons = {c.get("code"): c for c in pm.get("coupons", [])}
unique("promotions.coupons", [(c.get("code") or "").upper() for c in pm.get("coupons", [])], "coupon code (case-insensitive)")
for code, c in coupons.items():
    w = f"promotions.coupons[{code}]"
    require_sample_note(w, c)
    no_marker(w, "description", c.get("description"))
    if c.get("description") is not None and len(c["description"]) > 200:
        err(w, "description must be at most 200 characters (Coupon.MaxDescriptionLength)")
    if not isinstance(c.get("isListed"), bool):
        err(w, "isListed must be true or false (CouponRequest)")
    if not COUPON_CODE.match(code or ""):
        err(w, "code must be 3-20 characters A-Z, 0-9 (PROMO-R19)")
    kind, value = c.get("discountType"), c.get("discountValue")
    if kind == "PERCENT":
        if not isinstance(value, int) or not 1 <= value <= 10000:
            err(w, "PERCENT discountValue must be 1..10000 bp (PROMO-R11)")
        if not isinstance(c.get("maxDiscountPaise"), int) or c["maxDiscountPaise"] <= 0:
            err(w, "PERCENT coupons need maxDiscountPaise (PROMO-R11)")
    elif kind == "FLAT":
        if not isinstance(value, int) or value <= 0:
            err(w, "FLAT discountValue must be > 0 paise (PROMO-R20)")
    elif kind == "FREE_DELIVERY":
        if value != 0:
            err(w, "FREE_DELIVERY discountValue must be 0 (PROMO-R20)")
    else:
        err(w, "discountType must be PERCENT, FLAT or FREE_DELIVERY")
    if not isinstance(c.get("minOrderPaise"), int) or c["minOrderPaise"] < 0:
        err(w, "minOrderPaise must be >= 0 (PROMO-R20)")
    for f in ("perCustomerLimit", "totalLimit"):
        if c.get(f) is not None and (not isinstance(c[f], int) or c[f] < 1):
            err(w, f"{f} must be null or >= 1 (PROMO-R20)")
    if c.get("firstOrderOnly") and c.get("perCustomerLimit") != 1:
        err(w, "firstOrderOnly coupons need perCustomerLimit 1 (PROMO-R17)")
    if c.get("cityCode") is not None and c["cityCode"] not in cities:
        err(w, "cityCode does not exist (PROMO-R22)")
    vf, vt = c.get("validFromOffsetDays"), c.get("validToOffsetDays")
    if not isinstance(vf, int) or not isinstance(vt, int) or vt <= vf or vt <= 0:
        err(w, "validToOffsetDays must be after validFromOffsetDays and in the future (PROMO-R20)")
    for s in c.get("scopes", []):
        ref = s.get("scopeRef")
        if s.get("scopeType") == "CATEGORY" and ref not in categories:
            err(w, f"scope category '{ref}' does not exist (PROMO-R22)")
        elif s.get("scopeType") == "PRODUCT" and ref not in products:
            err(w, f"scope product '{ref}' does not exist (PROMO-R22)")
        elif s.get("scopeType") not in ("CATEGORY", "PRODUCT"):
            err(w, "scopeType must be CATEGORY or PRODUCT")

banners = {b.get("key"): b for b in pm.get("banners", [])}
unique("promotions.banners", [b.get("key") for b in pm.get("banners", [])], "banner key")
for key, b in banners.items():
    w = f"promotions.banners[{key}]"
    require_sample_note(w, b)
    for f in ("title", "subtitle"):
        no_marker(w, f, b.get(f))
    if not (b.get("title") or "").strip() or len(b["title"].strip()) > 120:
        err(w, "title is required and must be at most 120 characters (Promotion.MaxTitleLength)")
    if b.get("subtitle") is not None and len(b["subtitle"]) > 200:
        err(w, "subtitle must be at most 200 characters (Promotion.MaxSubtitleLength)")
    if not isinstance(b.get("sortOrder"), int) or not isinstance(b.get("isActive"), bool):
        err(w, "sortOrder must be an integer and isActive true or false (PromotionRequest)")
    target = b.get("linkTarget")
    lt = b.get("linkType")
    if (lt == "COUPON" and target not in coupons) or (lt == "CATEGORY" and target not in categories) or \
            (lt == "PRODUCT" and target not in products) or lt not in ("COUPON", "CATEGORY", "PRODUCT"):
        err(w, f"link {lt} '{target}' does not resolve (PROMO-R25)")
    if b.get("cityCode") is not None and b["cityCode"] not in cities:
        err(w, "cityCode does not exist")
    s, e = b.get("startsAtOffsetHours"), b.get("endsAtOffsetHours")
    if not isinstance(s, int) or (e is not None and (not isinstance(e, int) or e <= s)):
        err(w, "endsAtOffsetHours must be null or after startsAtOffsetHours")

# ------------------------------------------------------------------ identity and customers
idn = files.get("identity.json", {})
staff_emails = []
for s in idn.get("staff", []):
    w = f"identity.staff[{s.get('key')}]"
    require_sample_note(w, s)
    email = s.get("email") or ""
    staff_emails.append(email.lower())
    if not EMAIL.match(email) or not email.endswith(RESERVED_EMAIL_DOMAINS):
        err(w, "email must be valid and on a reserved domain (.example, .invalid, .test)")
    for rc in s.get("roleCodes", []):
        if rc not in role_codes:
            err(w, f"role '{rc}' is not in reference roles")
    if not s.get("roleCodes"):
        err(w, "at least one role is required")
    for lc in s.get("locationCodes", []):
        if lc not in locations:
            err(w, f"locationCode '{lc}' does not exist")
    if not s.get("allLocations") and not s.get("locationCodes"):
        err(w, "allLocations false needs locationCodes")
    if any("password" in k.lower() for k in s):
        err(w, "sample files never carry passwords")
unique("identity.staff", staff_emails, "staff email")

cu = files.get("customers.json", {})
phones = []
for c in cu.get("customers", []):
    w = f"customers.customers[{c.get('key')}]"
    require_sample_note(w, c)
    no_marker(w, "fullName", c.get("fullName"))
    phone = c.get("phone") or ""
    phones.append(phone)
    if not E164.match(phone) or not TEST_PHONE.match(phone):
        err(w, "phone must be E.164 in the fake block +919999900NNN (S-D3)")
    email = c.get("email")
    if email is not None and (not EMAIL.match(email) or not email.endswith(RESERVED_EMAIL_DOMAINS)):
        err(w, "email must be null or valid on a reserved domain (.example, .invalid, .test)")
    addresses = c.get("addresses", [])
    if len(addresses) > 20:
        err(w, "at most 20 active addresses (CUS-R3)")
    if addresses and sum(1 for a in addresses if a.get("isDefault")) != 1:
        err(w, "exactly one default address (CUS-R4)")
    if not any(a.get("pincode") in served_pincodes for a in addresses):
        err(w, "at least one address should be serviceable so the customer can check out")
    for j, a in enumerate(addresses):
        aw = f"{w}.addresses[{j}]"
        if a.get("label") not in ("HOME", "WORK", "OTHER"):
            err(aw, "label must be HOME, WORK or OTHER (CUS-R5)")
        for f in ("recipientName", "phone", "line1", "locality", "cityName", "stateCode", "pincode"):
            if not a.get(f):
                err(aw, f"{f} is required (CUS-R5)")
            no_marker(aw, f, a.get(f))
        if not TEST_PHONE.match(a.get("phone") or ""):
            err(aw, "phone must use the fake block +919999900NNN (S-D3)")
        if not STATE.match(a.get("stateCode") or ""):
            err(aw, "stateCode must be two upper-case letters")
        if not PINCODE.match(a.get("pincode") or ""):
            err(aw, "pincode must be 6 digits not starting with 0")
unique("customers.customers", phones, "customer phone")
overlap = set(phones) & {r.get("phone") for r in riders.values()}
for ph in overlap:
    err("customers/delivery", f"phone {ph} used by both a customer and a rider")

# ------------------------------------------------------------------ settings
st = files.get("settings.json", {})
bp = st.get("businessProfile")
if bp is None:
    err("settings.json", "businessProfile is required (invoices need a seller GSTIN, ORD-R52)")
else:
    w = "settings.businessProfile"
    require_sample_note(w, bp)
    for f in ("legalName", "tradeName", "address"):
        if not bp.get(f):
            err(w, f"{f} is required")
    if not gstin_valid(bp.get("gstin") or ""):
        err(w, "gstin must pass the Settings format and check-character rule (Gstins.IsValid)")
    if bp.get("fssaiLicenseNo") is not None and not re.match(r"^[0-9]{14}$", bp["fssaiLicenseNo"]):
        err(w, "fssaiLicenseNo must be null or 14 digits")
    if not TEST_PHONE.match(bp.get("supportPhone") or ""):
        err(w, "supportPhone must be E.164 in the fake block +919999900NNN (S-D3)")
    email = bp.get("supportEmail") or ""
    if not EMAIL.match(email) or not email.endswith(RESERVED_EMAIL_DOMAINS):
        err(w, "supportEmail must be valid and on a reserved domain (.example, .invalid, .test)")
    if not bp.get("timeZone"):
        err(w, "timeZone is required (IANA name)")
    if bp.get("onlyWhenMissing") is not True:
        err(w, "onlyWhenMissing must be true so a real business profile is never replaced")

# ------------------------------------------------------------------ images
im = files.get("images.json", {})
image_products = {}
for entry in im.get("products", []):
    slug = entry.get("productSlug")
    w = f"images.products[{slug}]"
    if slug not in products:
        err(w, "productSlug does not exist")
    image_products[slug] = entry
    imgs = entry.get("images", [])
    if not 1 <= len(imgs) <= 8:
        err(w, "1 to 8 images per product (CAT-R16)")
    for img in imgs:
        if not img.get("altText"):
            err(w, "altText is required (CAT-R16)")
        no_marker(w, "altText", img.get("altText"))
        ph = img.get("placeholder") or {}
        if min(ph.get("width", 0), ph.get("height", 0)) < 800:
            err(w, "placeholder must be at least 800 px on the shortest side (CAT-R16)")
for slug, p in products.items():
    if p.get("targetStatus") == "ACTIVE" and slug not in image_products:
        err(f"catalog.products[{slug}]", "an ACTIVE product needs an image entry in images.json (CAT-R3)")
for entry in im.get("categories", []):
    if entry.get("categorySlug") not in categories:
        err(f"images.categories[{entry.get('categorySlug')}]", "categorySlug does not exist")
for entry in im.get("banners", []):
    if entry.get("bannerKey") not in banners:
        err(f"images.banners[{entry.get('bannerKey')}]", "bannerKey does not exist")

# ------------------------------------------------------------------ city readiness (LOC-R6)
for code, city in cities.items():
    if city.get("targetStatus") != "ACTIVE":
        continue
    city_locations = [l for l in locations.values() if l.get("cityCode") == code and l.get("targetStatus") == "ACTIVE"]
    if not city_locations:
        err(f"readiness[{code}]", "FULFILLMENT_LOCATION_MISSING")
    for l in city_locations:
        lcode = l["code"]
        if not hours_by_location.get(lcode):
            err(f"readiness[{lcode}]", "operating hours for at least one day are required (LOC-R11)")
        if not any(a.get("locationCode") == lcode and a.get("pincodes") for a in service_areas.values()):
            err(f"readiness[{lcode}]", "SERVICE_AREA_MISSING / PINCODE_MISSING")
        sellable = [sku for sku, (p, v) in variants.items()
                    if v.get("isActive") and p.get("targetStatus") == "ACTIVE" and (lcode, sku) in priced
                    and p.get("taxCategoryCode") in tax_rules
                    and any((lcode, sku, x["code"]) in charged for x in v["preparations"] if x.get("isDefault"))]
        if not sellable:
            err(f"readiness[{lcode}]", "PRICE_MISSING")

# ------------------------------------------------------------------ result
if errors:
    for line in errors:
        print(line)
    print(f"\n{len(errors)} problem(s) in {SAMPLE_DIR}")
    sys.exit(1)

print(
    f"OK: {len(files)} files; {len(cities)} city, {len(locations)} location, {len(service_areas)} service areas, "
    f"{len(all_pincodes)} pincodes; {len(categories)} categories, {len(products)} products, {len(variants)} variants; "
    f"{len(priced)} prices, {len(charged)} preparation charges, {len(tax_rules)} tax rules; "
    f"{len(stock_items)} stock items, {len(inv.get('receipts', []))} batches; "
    f"{len(dl.get('slotTemplates', []))} slot templates, {len(riders)} riders; {len(coupons)} coupons, "
    f"{len(banners)} banners; {len(cu.get('customers', []))} customers, {len(idn.get('staff', []))} staff; business profile {'yes' if bp else 'no'}."
)
