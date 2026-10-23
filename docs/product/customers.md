# Customers module

Owner: E1. Schema `customers`. Uses `Identity.Public` (and `Catalog.Public`, not yet needed). Endpoints:
`docs/api/README.md` §4.2, §4.19. Error codes: `docs/api/README.md` §2 (Customers, Common).

## 1. Aggregates and boundaries

| Aggregate root | Tables | Children | Row version |
|---|---|---|---|
| Customer | `customers` | — | `customers.xmin`; every address change also stamps `customers.addresses_changed_at`, so concurrent address edits of one customer race on this row |
| Address | `addresses` | — | `addresses.xmin` (If-Match on PUT) |
| WishlistItem | `wishlist_items` | — | none (insert / delete only) |

| Boundary rule |
|---|
| The customer is created inside the Identity login transaction through `ICustomerAccountDirectory` (Identity.Public), implemented here. |
| Phone and account status live on `identity.users`; Customers reads them through `IIdentityApi` and changes them only through `SetUserBlockedAsync` / `AnonymiseUserAsync` (tracked, saved in our transaction). |
| Account deletion asks every registered `ICustomerDeletionCheck` (interface in `Customers.Public`, to be implemented by Ordering) for blocking reasons. |

### Public API (`Customers.Public`)

| Member | Returns | Used by |
|---|---|---|
| `ICustomersApi.GetCustomerAsync(customerId)` | `CustomerSummary` (name, phone, email, status) | Ordering, Payments |
| `ICustomersApi.GetDeliveryAddressAsync(customerId, addressId)` | active address of that customer, else null | Ordering (checkout snapshot) |
| `ICustomersApi.FindCustomerIdByUserAsync(userId)` | customer id or null | Cart |
| `ICustomerDeletionCheck` (implemented by other modules) | UPPER_SNAKE blocking reasons (e.g. `ACTIVE_ORDERS_EXIST`) | Ordering implements |

## 2. States

| Entity | States | Transitions |
|---|---|---|
| Customer | ACTIVE, DELETED | ACTIVE → DELETED by `DELETE /me` (terminal). Blocking is the user status (`DISABLED`), shown as `isBlocked`. |
| Address | active, inactive | `DELETE` deactivates (never hard-deleted: orders keep their own snapshot). |

## 3. Rules

| Key | Rule |
|---|---|
| CUS-R1 | A DELETED customer gets 403 `CUSTOMER_DELETED` on every `/me` endpoint (the token may still be valid for 15 min). |
| CUS-R2 | `PATCH /me` changes only the fields sent (`null` = unchanged); `fullName` 1–100 chars, `email` valid and ≤ 254. If-Match required. |
| CUS-R3 | At most 20 active addresses → 422 `ADDRESS_LIMIT_REACHED`. |
| CUS-R4 | Exactly one default among active addresses when there is at least one: the first address becomes default; `isDefault=true` or `POST …/default` moves the default; deleting the default promotes the newest remaining address. The partial unique index `(customer_id) where is_default and is_active` is the last line. |
| CUS-R5 | Address fields: label HOME/WORK/OTHER, recipientName, phone (E.164), line1, locality, cityName, stateCode (2 letters), pincode (6 digits) required; latitude −90..90, longitude −180..180 when given. Non-serviceable pincodes are accepted; `isServiceable` is null until Customers may use `Locations.Public` (see report). |
| CUS-R6 | Wishlist: PUT is idempotent (an existing item is kept), DELETE of a missing item is 204. |
| CUS-R7 | Delete account: refused while any `ICustomerDeletionCheck` reports a reason → 409 `INVALID_STATUS_TRANSITION`; otherwise status DELETED, name/email cleared, marketing off, addresses deactivated and their personal fields replaced, wishlist removed, user anonymised (phone cleared, DISABLED, tokens revoked). |
| CUS-R8 | Admin block/unblock (customers.manage, If-Match on the customer): user DISABLED / ACTIVE; blocking revokes refresh tokens; a DELETED customer cannot be unblocked (409 `INVALID_STATUS_TRANSITION`). |

## 4. Events

| Event | Payload | Consumers |
|---|---|---|
| `CustomerRegistered` | customerId, userId | Reporting (reads tables), future welcome message |
| `CustomerDeleted` | customerId, userId, deletedAt | Cart (drop active cart), Notifications (drop device tokens) — after the event rule allows it |
| `CustomerBlocked` / `CustomerUnblocked` | customerId, userId, actorId | Audit |

## 5. Commands

| Command | Endpoint | Rules | Writes |
|---|---|---|---|
| UpdateProfile | `PATCH /me` | R1, R2 | Customer |
| DeleteAccount | `DELETE /me` | R1, R7 | Customer, Addresses, Wishlist, User (via Identity) |
| AddAddress / ReplaceAddress / DeleteAddress / MakeDefault | `/me/addresses…` | R3–R5 | Address(es), Customer (`addresses_changed_at`) |
| AddToWishlist / RemoveFromWishlist | `/me/wishlist/{productId}` | R6 | WishlistItem |
| BlockCustomer / UnblockCustomer | `/admin/customers/{id}/block|unblock` | R8 | Customer, User (via Identity) |
