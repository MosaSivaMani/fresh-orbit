# Locations module

Owner: E2. Schema `locations`. Uses no other module's `.Public`. Endpoints: `docs/api/README.md` §4.3, §4.18.
Error codes: `docs/api/README.md` §2 (Locations, Common).

## 1. Aggregates and boundaries

| Aggregate root | Tables | Children | Row version |
|---|---|---|---|
| City | `cities` | — | `cities.xmin` |
| FulfillmentLocation | `fulfillment_locations` | `operating_hours`, `location_closures` | `fulfillment_locations.xmin` |
| ServiceArea | `service_areas` | `pincodes` | `service_areas.xmin` |

| Boundary rule |
|---|
| A ServiceArea references its City and FulfillmentLocation by id only; consistency (same city) is checked in the command that creates or changes it. |
| City activation reads other modules through checks they implement (§5); Locations never references Pricing, Inventory, Delivery or Ordering. |
| Pincode uniqueness across service areas is enforced by the partial unique index `unique (pincode) where is_active`; the command checks first, the index is the last line. |
| Slots, riders and fees charged on orders belong to Delivery / Ordering; Locations only stores the service-area fee configuration. |

### Public API (`Locations.Public`)

| Member | Returns | Used by |
|---|---|---|
| `ResolvePincodeAsync(pincode)` | `LocationContext { cityId, cityCode, timeZone, fulfillmentLocationId, fulfillmentLocationCode, serviceAreaId, deliveryFeePaise, freeDeliveryAbovePaise, minOrderPaise }` or error `PINCODE_NOT_SERVICEABLE` | Api host (X-Pincode), Cart, Ordering, Customers, Catalog |
| `GetLocationsAsync(ids)` | id, code, name, cityId, status, timeZone, hasColdStorage, fssaiLicenseNo | Pricing, Inventory, Delivery, Ordering |
| `GetClosuresAsync(locationId, fromDate, toDate)` | closed dates | Delivery (slot generation) |
| `GetOperatingHoursAsync(locationIds)` | day → opens/closes | Delivery |
| `ICityActivationCheck` (interface implemented by other modules) | `IReadOnlyList<MissingItem>` for given location ids | Pricing, Inventory, Delivery implement |
| `ICityRetirementCheck` (interface implemented by other modules) | blocking reasons | Ordering implements |

## 2. States and transitions

### City (`cities.status`)

| From | To | Command / endpoint | Guard |
|---|---|---|---|
| — | DRAFT | CreateCity `POST /admin/cities` | code unique |
| DRAFT | ACTIVE | ActivateCity `POST /admin/cities/{id}/activate` | LOC-R6 readiness, `cities.activate` |
| PAUSED | ACTIVE | ActivateCity | LOC-R6 readiness (re-checked) |
| ACTIVE | PAUSED | PauseCity `POST …/pause` | — |
| DRAFT, PAUSED | RETIRED | RetireCity `POST …/retire` | LOC-R9 |
| ACTIVE | RETIRED | refused | pause first |
| RETIRED | any | refused | terminal |

Any other transition → 409 `INVALID_STATUS_TRANSITION`.

### FulfillmentLocation (`fulfillment_locations.status`)

| From | To | Command / endpoint | Guard |
|---|---|---|---|
| — | DRAFT | CreateFulfillmentLocation `POST /admin/fulfillment-locations` | city not RETIRED |
| DRAFT | ACTIVE | UpdateFulfillmentLocation `PUT /admin/fulfillment-locations/{id}` (`status`) | LOC-R11 |
| ACTIVE | PAUSED | same | LOC-R12 |
| PAUSED | ACTIVE | same | LOC-R11 |
| PAUSED | DRAFT, ACTIVE → DRAFT | refused | — |

### ServiceArea and Pincode

| Item | States | Rule |
|---|---|---|
| ServiceArea | `isActive` true/false | Deactivating keeps pincodes but they stop resolving |
| Pincode | `isActive` true/false | Removal sets `isActive=false`; history kept; re-adding a pincode creates a new row |

## 3. Business rules

| Key | Rule |
|---|---|
| LOC-R1 | **Serviceability resolution.** pincode → `pincodes` (active) → `service_areas` (active) → `fulfillment_locations` (ACTIVE) → `cities` (ACTIVE). Any break → `PINCODE_NOT_SERVICEABLE`. One query, no loop. |
| LOC-R2 | Pincode format `^[1-9][0-9]{5}$`; otherwise 422 `VALIDATION_FAILED` on `pincode`. |
| LOC-R3 | A pincode is active in at most one service area (`PINCODE_ALREADY_ASSIGNED`). Moving a pincode = remove, then add. |
| LOC-R4 | A service area's fulfillment location must belong to the same city as the service area. |
| LOC-R5 | Fees: `deliveryFeePaise ≥ 0`; `minOrderPaise ≥ 0`; `freeDeliveryAbovePaise` null or `≥ minOrderPaise`. |
| LOC-R6 | **City readiness** (activation refused with 422 `CITY_NOT_READY` and the full `missing` list, never the first item only). For the city: ≥1 ACTIVE fulfillment location (`FULFILLMENT_LOCATION_MISSING`). For each ACTIVE location: ≥1 active service area (`SERVICE_AREA_MISSING`) with ≥1 active pincode (`PINCODE_MISSING`); plus each `ICityActivationCheck`: Delivery ≥1 active slot template (`SLOT_TEMPLATE_MISSING`), Pricing ≥1 sellable priced variant (`PRICE_MISSING`, see PRC-R12), Inventory ≥1 stock item (`STOCK_ITEM_MISSING`). |
| LOC-R7 | `GET /admin/cities/{id}/readiness` runs the same checks as LOC-R6 and never writes. |
| LOC-R8 | Pausing a city: no new carts priced, no new orders (`PINCODE_NOT_SERVICEABLE` / `CITY_NOT_ACTIVE`); placed orders continue to delivery. |
| LOC-R9 | Retire allowed only from DRAFT or PAUSED and only when every `ICityRetirementCheck` returns nothing (Ordering: no order in a non-terminal status). |
| LOC-R10 | `cities.code` UPPER_SNAKE, immutable after create; `stateCode` 2-letter Indian state code; `timeZone` IANA name, default `Asia/Kolkata` from data, never from code. No city names in code. |
| LOC-R11 | A location can become ACTIVE only with: address, pincode, latitude/longitude, `fssaiLicenseNo`, `hasColdStorage = true`, operating hours for ≥1 day; city not RETIRED. |
| LOC-R12 | Pausing the last ACTIVE location of an ACTIVE city is refused (`INVALID_STATUS_TRANSITION`); pause the city first. |
| LOC-R13 | Operating hours: replaced as a whole week (7 rows max); `opensAt < closesAt`, same day; a missing day = closed that day. |
| LOC-R14 | Closures: date ≥ today (location time zone); one per date (`DUPLICATE`); only future closures can be removed. |
| LOC-R15 | Fulfillment location `code` UPPER_SNAKE / hyphen (`CHN-TVM`), unique, immutable. |
| LOC-R16 | Resolution results are cached in memory for 60 s per pincode; any Locations event below clears the cache. |
| LOC-R17 | All times in UTC; dates and times of day (`closed_on`, `opens_at`) are in the location's city time zone. |

Website note (E2E-6): the website footer's "Cities we serve" list comes from `GET /cities` and is cached for up to
5 minutes, so a city that was just activated, paused or retired shows there within about 5 minutes. Serviceability,
prices and checkout never use that cache: they are checked on every request. Details are in `design/web.md` §5.

## 4. Events

| Event (raised) | Payload | Consumers |
|---|---|---|
| `CityActivated` | cityId, code, activatedAt, activatedBy | Audit, Notifications (staff alert) |
| `CityPaused` | cityId | Audit, Cart (marks carts in city unpriceable on read, no write) |
| `CityRetired` | cityId | Audit |
| `FulfillmentLocationStatusChanged` | locationId, from, to | Delivery (stop/resume slot generation), Audit |
| `LocationClosureAdded` | locationId, closedOn | Delivery (close slots that date; report booked orders to support) |
| `LocationClosureRemoved` | locationId, closedOn | Delivery (regenerate slots) |
| `OperatingHoursChanged` | locationId | Delivery |
| `ServiceAreaChanged` | serviceAreaId, fees, isActive | Audit |
| `PincodesChanged` | serviceAreaId, added[], removed[] | Audit |

Consumed: none.

## 5. Commands (Read → Decide → Commit → Follow-on)

| Command | Endpoint | Read | Decide | Commit | Follow-on |
|---|---|---|---|---|---|
| CreateCity | `POST /api/v1/admin/cities` | code exists? | LOC-R10 | insert city DRAFT | audit |
| UpdateCity | `PUT /api/v1/admin/cities/{cityId}` (IM) | city | LOC-R10, version | update name, time zone | audit |
| ActivateCity | `POST …/cities/{cityId}/activate` (IM) | city + locations + areas + pincode counts (one query); each `ICityActivationCheck` once with all location ids | status DRAFT/PAUSED; LOC-R6 missing list empty | status ACTIVE, `activated_at`, `activated_by` | `CityActivated` |
| PauseCity | `POST …/pause` (IM) | city | ACTIVE | status PAUSED | `CityPaused` |
| RetireCity | `POST …/retire` (IM) | city; `ICityRetirementCheck` | LOC-R9 | status RETIRED | `CityRetired` |
| CreateFulfillmentLocation | `POST /api/v1/admin/fulfillment-locations` | city, code exists? | LOC-R15 | insert DRAFT | audit |
| UpdateFulfillmentLocation | `PUT …/fulfillment-locations/{id}` (IM) | location, city, sibling ACTIVE count | LOC-R11, LOC-R12 | update fields / status | `FulfillmentLocationStatusChanged` if status changed |
| SetOperatingHours | `PUT …/{id}/operating-hours` (IM) | location | LOC-R13 | replace rows | `OperatingHoursChanged` |
| AddClosure | `POST …/{id}/closures` | location, existing closure | LOC-R14 | insert | `LocationClosureAdded` |
| RemoveClosure | `DELETE …/{id}/closures/{closureId}` | closure | future only | delete row (future, never effective) | `LocationClosureRemoved` |
| CreateServiceArea | `POST /api/v1/admin/service-areas` | city, location | LOC-R4, LOC-R5 | insert | `ServiceAreaChanged` |
| UpdateServiceArea | `PUT …/service-areas/{id}` (IM) | area | LOC-R5 | update | `ServiceAreaChanged` |
| AddPincodes | `POST …/service-areas/{id}/pincodes` (IM area) | area; active rows for all given pincodes (one `IN` query) | LOC-R2, LOC-R3 for every pincode; all-or-nothing, errors keyed `pincodes[i]` | insert rows | `PincodesChanged` |
| RemovePincode | `DELETE …/pincodes/{pincode}` (IM area) | area + row | active | `is_active=false` | `PincodesChanged` |
| ResolvePincode (query) | `GET /api/v1/serviceability`, X-Pincode | LOC-R1 query | — | — | — |

`GET /serviceability` `nextSlot` comes from Delivery through an `INextSlotProvider` interface declared in `Locations.Public` and implemented by Delivery (see Q2).

## 6. Test scenarios

### Unit (domain)

| # | Scenario | Expect |
|---|---|---|
| U1 | Activate DRAFT city with empty missing list | ACTIVE, `CityActivated` raised |
| U2 | Activate with 3 missing items | `CITY_NOT_READY`, all 3 listed |
| U3 | Activate RETIRED / ACTIVE city | `INVALID_STATUS_TRANSITION` |
| U4 | Retire ACTIVE city | refused |
| U5 | Location → ACTIVE without FSSAI number | `VALIDATION_FAILED` on `fssaiLicenseNo` |
| U6 | Pause last ACTIVE location of ACTIVE city | `INVALID_STATUS_TRANSITION` |
| U7 | `freeDeliveryAbovePaise` < `minOrderPaise` | `VALIDATION_FAILED` |
| U8 | Hours with `opensAt ≥ closesAt` | `VALIDATION_FAILED` |
| U9 | Closure in the past | `VALIDATION_FAILED` |
| U10 | Pincode `012345`, `60004`, `6000411` | `VALIDATION_FAILED` |

### Integration (Postgres, Testcontainers)

| # | Scenario | Expect |
|---|---|---|
| I1 | Resolve active pincode | full `LocationContext` |
| I2 | Resolve with area inactive / location PAUSED / city PAUSED / unknown pincode | `PINCODE_NOT_SERVICEABLE` each |
| I3 | Add pincode already active elsewhere | `PINCODE_ALREADY_ASSIGNED`; nothing inserted (all-or-nothing batch) |
| I4 | Two concurrent AddPincodes with the same pincode | one succeeds, other 409 `CONCURRENT_SAVE` / `PINCODE_ALREADY_ASSIGNED` (index backstop) |
| I5 | Activate city with fake `ICityActivationCheck` implementations returning items | 422 body lists them with location ids |
| I6 | Activate with stale `If-Match` | 412 `CONCURRENCY_CONFLICT`; missing → 428 |
| I7 | `GET /serviceability?pincode=600123` (not served) | 200 `serviceable:false` |
| I8 | Add closure → outbox row `LocationClosureAdded` in same transaction | outbox row present |
| I9 | Resolution query count | 1 SQL statement per resolve |
| I10 | Retire with fake retirement check returning open orders | 422, status unchanged |

## 7. Open questions (with proposed answers)

| # | Question | Proposed answer |
|---|---|---|
| Q1 | Readiness needs Pricing, Inventory, Delivery data but Locations uses no `.Public`. | `ICityActivationCheck` declared in `Locations.Public`, implemented in each module's Infrastructure, registered in DI (dependency inversion; no project cycle). |
| Q2 | `/serviceability` returns `nextSlot` (Delivery). | `INextSlotProvider` in `Locations.Public`, implemented by Delivery; null if none. |
| Q3 | Should readiness include Settings (business profile GSTIN, order policy)? | Yes later: add `BUSINESS_PROFILE_INCOMPLETE`, `ORDER_POLICY_MISSING` via a Settings check; needs two new codes in `docs/api`. |
| Q4 | Closure added on a date with booked orders. | Allowed; Delivery closes slots and raises a support task listing affected orders; orders are not auto-cancelled. |
| Q5 | Polygon / radius service areas instead of pincodes? | Not in v1; pincode lists only. `latitude/longitude` kept for later. |
| Q6 | Can one pincode be served by two locations (overflow)? | No (LOC-R3). Revisit when a city has two stores. |

**Implementation notes (Phase 2).** D30: events other modules consume (city activated/paused/retired, fulfillment-location status and closure added/removed, service-area and pincode changes) live in `Locations.Public/Events`; `Locations.Domain` references `Locations.Public` to raise them. `ILocationsApi.GetClosuresAsync(fulfillmentLocationId, from, to)` returns closed dates for Delivery. Retiring a city is refused with `CITY_RETIREMENT_BLOCKED` when any `ICityRetirementCheck` (in `Locations.Public`; e.g. Ordering for open orders) reports blocking items, listed in `blocking`; only a `PAUSED` city can be retired.
