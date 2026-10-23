# Payment options (evaluated 2026-10-23)

Decision: D26 in `docs/decisions/log.md`. No payment gateway is free: moving money is a regulated service. Fees change; check current pricing.

| Option | Cost | Needs | Fit |
|---|---|---|---|
| Cash / UPI on delivery | Free | Nothing | **Launch choice.** Customer pays the final weighed amount at the door |
| Own UPI QR / UPI ID (no gateway) | Free for normal UPI | Bank current account | Works but staff verify each payment by hand; no automatic callback |
| Gateway test mode (Razorpay, Cashfree, PayU, PhonePe PG) | Free | Developer account | Build and test the full online flow at no cost |
| Gateway live mode | Usually no setup fee; per-transaction fee (typically around 2% on cards/netbanking; UPI varies) | Business KYC, bank account, GST, live site with policies | Needed for online prepayment |
| Hyperswitch (open-source orchestrator) | Software free | Still needs a gateway | Only useful with several gateways |

## Plan
| Phase | Payments |
|---|---|
| Launch | COD and UPI on delivery (`IPaymentProvider`: CashOnDelivery, UpiOnDelivery) |
| Development | Fake provider with signed webhooks for tests; gateway adapter in test mode |
| After KYC | Gateway live; prepaid charges the estimate, shortfall refunded, overweight capped at captured amount (D15) |

## COD risk controls
OTP-verified phone login, COD maximum order value (₹5,000 default, D17), repeat no-show blocking (later).
