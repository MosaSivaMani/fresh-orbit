> **DRAFT for owner and lawyer review. Not published. Not legal advice.**
> Written from what the system does today: `docs/product/ordering.md` (ORD-R18, R27–R31, R34–R42, R54–R58),
> `docs/product/payments.md` (PAY-R3, R4, R8, R12–R18), `docs/product/delivery.md` (DEL-R16), decisions D15, D17, D19,
> D26 and the default order policy in `backend/data/reference/settings/order-policies.json`. Numbers marked
> *(setting)* are order-policy settings the business can change in the admin; if one changes, this page must change
> in the same week. Replaces the earlier draft `docs/content/cancellation-refund-policy.md`.
> Fill every `{{OWNER: …}}`. Points marked **Check with lawyer:** must be cleared before publishing.

# Refund and cancellation policy

Last updated: {{OWNER: date of publication}}

This policy is part of our [Terms of use](terms-of-use.md). It applies to orders placed with
{{OWNER: legal name of the business}} ("Fresh Orbit", "we", "us") on our website or app.

Fresh fish and seafood are perishable. We clean and cut every order to your choice, so the rules below depend on how
far your order has gone. Nothing in this policy takes away any right you have under consumer law.

## 1. Cancelling an order

You can see your order's status in the order screen. The **Cancel** button is shown only while you are allowed to
cancel.

| Order status | Can you cancel it yourself? | What happens |
|---|---|---|
| **Confirmed** (placed, not yet being prepared) | Yes | The order is cancelled. Nothing is charged. |
| **Preparing** (we have started cleaning and cutting) | No *(setting: the business may allow it)* | Contact support. We can still cancel at this stage, but we cannot promise to. |
| **Packed** | No | Contact support. We may cancel in exceptional cases. |
| **Out for delivery** and later | No | The order can no longer be cancelled. If something is wrong, see §4. |
| **Awaiting payment** (online payment, when we offer it) | Yes | The order is cancelled and nothing is charged. |

- **No cancellation fee.** We do not charge you for cancelling.
- When you cancel, we ask for a reason: changed my mind, wrong address, slot does not suit me, or other.
- **If you used a coupon**, the coupon use is given back when an order is cancelled, so you can use it again within
  its dates and limits.
- **Online payment (when we offer it):** an order not paid within **15 minutes** *(setting)* is cancelled
  automatically and nothing is charged.

### When we cancel

We may cancel an order ourselves, up to the time it is packed, for example when:

- an item is out of stock and you asked us not to substitute;
- the delivery address cannot be served or is incomplete;
- we find a quality problem while preparing your fish;
- you asked support to cancel.

We tell you the reason by SMS and in the app. If you have paid anything, we refund it in full (§5).

**Check with lawyer:** Consumer Protection (E-Commerce) Rules 2020, rule 4(6): if we ever charge customers for
cancelling, we must bear similar charges when we cancel. We charge nothing today; keep it that way or add the matching
clause.

## 2. Weight: why the final price can differ

Fish and shellfish are sold by **net weight after cleaning and cutting**. Nobody can cut fish to an exact weight, so the
weight is measured when we pack your order.

- At checkout you see the **estimated total** (for the weight you ordered) and the **most you could pay**.
- **If we pack more than you ordered**, you pay for the packed weight, but **never more than 10 % above** the weight
  you ordered *(setting)*. Any fish above that limit is free.
- **If we pack less than you ordered**, you pay only for what we packed.
- Items sold **per piece or per pack** have no weight difference.
- After packing, the app shows the **final amount**; the rider's SMS repeats the amount to pay. This is the amount you pay on delivery and the amount on
  your invoice.
- **Paid online (when we offer it):** you are never charged more than you paid. If the final amount is lower, the
  difference is refunded automatically to your original payment method.

**Check with lawyer:** whether charging up to 10 % above the ordered weight, shown as a "most you could pay" amount at
checkout, is clear enough consent under the Consumer Protection Act 2019 and the Legal Metrology rules (see the
research note).

## 3. Items we cannot supply

At checkout you choose what we should do if an item is not available, or not good enough, when we prepare your order:

| Your choice | What happens |
|---|---|
| **Call me** | We offer a replacement item in the app and call you. You have **15 minutes** *(setting)* to accept. If you do not answer, the item is removed. |
| **Choose a similar item** | We replace it with a similar item only if its most-you-could-pay price is not higher than the original's. Otherwise we ask you, as for *Call me*. |
| **Don't substitute** | The item is removed. |

A removed item is not charged (pay on delivery) or is refunded automatically (paid online). A replacement you accept
is charged at its own price.

## 4. A problem with a delivered order

Report it from the order screen ("Report a problem") **within 24 hours of delivery** *(setting)*. After that the
button closes; contact support (§7) if something unusual happened, and see our
[Grievance redressal policy](grievance-redressal.md).

1. Choose the item and what is wrong: **missing**, **damaged**, **poor quality**, or **wrong item**. For a **late
   delivery**, choose the whole order.
2. Describe the problem (at least 10 characters).
3. Add **up to 4 photos** (JPEG, PNG or WebP, up to 5 MB each). Photos of the fish and the packing help us decide
   quickly.
4. {{OWNER: say whether customers must keep the product until the complaint is decided, and for how long}}

You can have one open complaint per item. Our team reviews every complaint and either:

- **approves it** and refunds an amount up to what you paid for that item (or for the whole order, for a late
  delivery){{OWNER: add ", or sends a replacement" only if replacements will be offered}}; or
- **rejects it** and tells you why.

We confirm that we received your complaint (in the app and by email if you gave us one) and send you an SMS when we
decide it.

**Check with lawyer:**
- Whether a 24-hour window is reasonable for perishable food, and how to word it so it does not appear to limit a
  consumer's legal rights (Consumer Protection (E-Commerce) Rules 2020, rule 7: an inventory seller may not refuse a
  refund for goods that are defective, deficient, not as described, or delivered late).
- Late delivery: the rules speak of refusing a refund when goods are "delivered late from the stated delivery
  schedule". Our system lets staff decide the amount. Confirm whether a full refund must be offered for late
  delivery, and what "late" means for a delivery slot.

## 5. How refunds are paid

The total refunded can never be more than you paid.

### Paid on delivery (cash or UPI to the rider's QR code) — how we accept payment at launch

- **Before you pay** nothing needs refunding: weight differences, removed items and goodwill credits simply lower the
  amount due, and a cancelled or returned order is not charged.
- **After you pay**, a cash or rider-QR payment cannot be reversed automatically. We pay the refund by:
  - **UPI transfer** to the UPI ID you give us;
  - **bank transfer** (we ask for your bank details outside the app and do not store them in our system); or
  - **cash** {{OWNER: confirm whether cash refunds will be offered; remove this line if not}}.
- Complaint and goodwill refunds are requested by one team member and **approved by a second person** before they are
  paid.
- When the refund is paid you get an SMS with the reference number (the UTR for UPI and bank transfers).
- **Timeline:** we pay approved refunds within {{OWNER: number}} working days of approval.

### Paid online (when we offer it)

- Refunds go back to **the original payment method** automatically.
- These refunds start without waiting for a person: cancellation of a paid order, weight shortfall, items not supplied,
  and a payment that arrived after the order was already closed.
- Complaint and goodwill refunds need a second person's approval, as above.
- **Timeline:** we start the refund {{OWNER: e.g. "the same day"}}; your bank or UPI app usually shows it within
  {{OWNER: number}} working days.

**Check with lawyer:** rule 4(10) of the Consumer Protection (E-Commerce) Rules 2020 requires refunds "within a
reasonable period" or as RBI prescribes; set the two `{{OWNER: number}}` values with that in mind.

## 6. Orders we could not deliver

If we cannot deliver (see the [Shipping and delivery policy](shipping-delivery-policy.md)), the rider can try again
the same day. After the last failed attempt the order comes back to the store:

- **Paid on delivery:** you are not charged.
- **Paid online (when we offer it):** the amount paid is refunded in full to the original payment method after our
  team reviews it. A coupon used on a returned order is **not** given back.

**Check with lawyer:** whether keeping the coupon used on a returned order is fair when the failure was not the
customer's fault.

## 7. Contact

- Support: {{OWNER: support phone}}, {{OWNER: support email}}, {{OWNER: support hours}}
- Grievance officer: see the [Grievance redressal policy](grievance-redressal.md).
