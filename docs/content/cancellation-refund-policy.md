> **Superseded (2026-10-24, D107):** the current draft is `docs/legal/refund-cancellation-policy.md`. Kept for history; do not edit.

> **DRAFT — for owner review. Not published.** This text must match the rules the system enforces. Sources:
> decisions D15 (refunds), D17 (policy defaults: complaint window 24 h, COD limit ₹5,000, payment window 15 min,
> substitution window 15 min), D19 (cart, promotions, ordering answers), D26 (cash / UPI on delivery at launch);
> `docs/product/ordering.md` ORD-R18, R27–R31, R34–R41, R54–R58; `docs/product/payments.md` PAY-R12–R18. Values
> marked *configurable* are settings (`order_policies`) and may be changed by the business; update this page when
> they change. Refund timelines are not decided yet (`[Owner: …]`). Needs legal review together with `terms.md`.

# Cancellation and refund policy

## 1. Cancelling an order

| Order status | Can you cancel in the app? | What happens |
|---|---|---|
| **Awaiting payment** (online payment not completed) | Yes | The order is cancelled; nothing is charged. |
| **Confirmed** (not yet being prepared) | Yes | The order is cancelled. Any amount already paid online is refunded in full. |
| **Preparing**, **Packed** | No — call support | We cut fish to order, so once preparation starts we may not be able to cancel. Support can cancel up to the *Packed* stage in exceptional cases. |
| **Out for delivery**, **Delivered** | No | See §4 (problems with your order). |

*Configurable:* the business may allow customer cancellation until *Preparing*. The app always shows a **Cancel**
button only when cancellation is allowed.

We may cancel an order ourselves, for example when an item is out of stock and you chose no substitute, for an
address problem, or for a quality issue found while preparing. You are told why, and anything you paid online is
refunded in full.

If you used a coupon on a cancelled order, the coupon use is returned and you can use it again (within its validity
and limits).

**Unpaid online orders** are cancelled automatically if payment is not completed within **15 minutes** *(configurable)*.

## 2. Weight adjustment (final price)

Fish and shellfish are sold by **net weight after cleaning and cutting**, so the exact weight is known only when we
pack your order.

- **Packed more than ordered:** you pay for the packed weight, capped at **10 % above** the ordered weight
  *(configurable)*. Anything above the cap is free.
- **Packed less than ordered:** you pay only for the packed weight.
- **Cash / UPI on delivery:** the amount you pay the rider is the final amount, shown in the app after packing.
- **Prepaid online (when available):** you are never charged more than you paid. If the final amount is lower, the
  difference is refunded automatically to the original payment method.

Items sold **per piece or per pack** have no weight adjustment.

## 3. Items we cannot supply (short supply and substitutions)

At checkout you choose what we should do if an item is not available or not good enough when we prepare your order:

| Your choice | What happens |
|---|---|
| **Call me** | We propose a substitute in the app and call you. You have **15 minutes** *(configurable)* to accept; if you do not answer, the item is removed. |
| **Choose a similar item** | We replace it with a similar item only if it does not cost more than the original's maximum price; otherwise we ask you as in *Call me*. |
| **Don't substitute** | The item is removed. |

A removed item is not charged (cash on delivery) or is refunded automatically (prepaid). An accepted substitute is
charged at its own price.

## 4. Problems with a delivered order (complaints)

Report a problem **within 24 hours of delivery** *(configurable)* from the order screen ("Report a problem"). After
24 hours the option closes; contact support if something unusual happened.

- Choose the item and the problem: **missing**, **damaged**, **quality**, **wrong item**, or **late delivery** (for
  the whole order).
- Describe the problem (at least 10 characters) and add up to **4 photos**. Photos help us decide quickly; please keep
  the product until the complaint is decided. [Owner: decide whether to ask customers to keep the product.]
- One open complaint per item.

Our team reviews every complaint. If approved, we refund an amount **up to the final price of that item** (or of the
order, for late delivery), or arrange a replacement [Owner: confirm whether replacements are offered]. If rejected,
we tell you why.

## 5. How refunds are paid

| How you paid | Refund method |
|---|---|
| **Online (when available)** | Back to the **original payment method** automatically. |
| **Cash on delivery / UPI on delivery** | We cannot reverse a cash or rider-QR payment, so we pay you by **UPI transfer** (you give us your UPI ID), **bank transfer**, or **cash** [Owner: confirm cash refunds]. |

- Automatic refunds (cancellation of a paid order, weight shortfall, items not supplied, a payment received after the
  order was closed) are started by the system without waiting for approval.
- Complaint and goodwill refunds are requested by our support team and **approved by a second person** before they
  are paid.
- The total refunded can never exceed what you paid.
- You get a notification when a refund is started and when it succeeds, with the reference number (for UPI and bank
  transfers, the UTR).
- **Timelines:** [Owner: e.g. "online refunds are started immediately; banks usually credit them in 5–7 working days";
  "UPI and bank transfers are made within N working days of approval".]

## 6. Returned and failed deliveries

If an order is returned to the store after failed delivery attempts (see the delivery policy):

- **Cash / UPI on delivery:** nothing is charged.
- **Prepaid (when available):** the full amount paid is refunded to the original payment method. A coupon used on a
  returned order is **not** given back.

## 7. Contact

[Owner: support phone, email, hours, and the grievance officer named in `terms.md`.]
