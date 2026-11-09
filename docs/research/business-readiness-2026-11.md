# Running Fresh Orbitt as a real, profitable business: gaps and plan

Written 2026-11-08 by the lead, at the owner's request: "think like a real-time, sustainable app; it needs to make
profit." The October gap audit (`gap-audit-2026-10/`) checked the **software**. This page checks the **business
around it**: what a seafood delivery store needs to earn money every day, keep customers and survive bad days,
and which of that the system already supports.

Status: **analysis and proposal.** Numbers marked *(estimate)* are rough planning figures, not facts about
Fresh Orbitt; the owner replaces them with real ones from the first weeks of trading.

## 1. How this business makes or loses money

A fresh-fish store lives or dies on **contribution per order**: what is left from one order after everything
that order cost, before rent and salaries.

```
Contribution per order =
    selling price (ex GST)
  − fish cost (what we paid at the harbour, incl. cleaning loss)
  − wastage share (fish bought but never sold)
  − packaging and ice
  − delivery cost (rider pay per drop, fuel)
  − payment cost (0 for cash; ~2% for online when switched on)
  − discounts and refunds
```

Then: **monthly profit = contribution per order × orders per month − fixed costs** (salaries, rent, server,
SMS, licences, marketing).

Two things decide whether it works:

1. **Wastage.** Unsold fish is a total loss within a day or two. Buying to match orders, not hope, is the
   biggest lever.
2. **Repeat customers.** Getting a customer costs money (ads, offers); a family that orders every week for a
   year pays that back many times. A store that only gets one-time orders does not survive.

### Example worksheet *(estimate: replace with real figures)*

| Line | Per order | Note |
|---|---|---|
| Average order, ex GST | ₹600 | 1–1.5 kg of fish and prawns |
| Fish cost incl. cleaning loss | −₹390 | 65 % of price |
| Wastage share | −₹30 | 5 % of sales |
| Packaging and ice | −₹20 | insulated bag, ice, label |
| Delivery cost | −₹45 | rider pay per drop in one area |
| Payment cost | ₹0 | cash or UPI on delivery |
| Discounts and refunds | −₹25 | first-order offer, weight refunds |
| **Contribution** | **₹90 (15 %)** | |

With fixed costs of *(estimate)* ₹1,20,000 a month (two staff, a rider, rent, tools), break-even is about
**1,350 orders a month, ~45 a day**. Every ₹10 saved on wastage or delivery per order lowers that by ~10 %.
The real numbers come from the system once the gaps in §2.1 are closed.

### Setting a price (D347, D348)

Since 2026-11-08 the price the customer sees is **the most they pay** (D347): we never charge for weight above
what was ordered, and a lighter pack costs less. Like Licious and FreshToHome, our top items will also be sold as
**fixed net-weight packs** at one price (D348), e.g. "Seer steaks, 500 g net (5–7 pieces), ₹X", packed to at least
the stated net weight. Both mean a pack is always a little over the stated weight and **we give that extra away**, so
it must be inside the price. Method, per product and preparation:

```
cost per net kg   = harbour price per kg (raw) ÷ cleaning yield        e.g. ₹700 ÷ 0.70 = ₹1,000
+ wastage         = × (1 + wastage share)                              e.g. × 1.05      = ₹1,050
+ over-pack give  = × (1 + over-pack giveaway, ~2–5 %)                 e.g. × 1.03      = ₹1,082
+ packaging       = + packaging and ice per kg                         e.g. + ₹30       = ₹1,112
price per net kg  = ÷ (1 − target margin)                              e.g. ÷ (1 − 0.30) = ₹1,589
pack price        = price per net kg × pack net kg                     e.g. × 0.5       = ₹794
```

Then **compare with the market** (Licious, FreshToHome, local shops for the same cut and net weight) and **round**
to a clean price (₹799, not ₹794); if the rounded price is far above the market, look again at buying and yield
before cutting the margin. The over-pack giveaway is measured, not guessed: the pack screen records the packed weight,
so (packed − ordered) ÷ ordered over a month gives the real figure to put back into the price. Prices are set per
location in the admin (price periods); there is no fallback price (rule 7).

## 2. Gaps, area by area

Legend: **Have** = in the system today · **Gap** = missing · priority **P1** (before or at soft launch),
**P2** (first 2 months), **P3** (months 3–6).

### 2.1 Money: knowing if we make profit

| Need | State | Priority |
|---|---|---|
| Fish cost per batch, cost of every sale and waste movement | **Have**: inventory batches carry cost; movements carry `cost_paise` (INV-R15) | — |
| Contribution margin and waste reports | **Have**: `contribution_margin`, `waste_summary`, `product_performance` (Reporting) | — |
| Delivery cost per order | **Built (2026-11-09, D365–D368, branch `business/order-costs`)**: rider pay per drop set per location in the admin, frozen on each shipment at delivery; the margin report subtracts it and says "not configured" where it is missing. Per-shift pay waits for the owner's choice (D368) | **P1** |
| Packaging cost per order | **Built (2026-11-09, D365–D367)**: a fixed packaging cost per order per location, frozen at delivery and subtracted in the margin report | P1 |
| Fixed costs and a monthly profit view | **Gap**: outside the system today. A simple monthly sheet first; an "expenses" list in admin later | P2 |
| Marketing cost per new customer | **Gap**: no record of where customers come from (no UTM or referral source) | P2 |
| Daily owner dashboard: orders, sales, contribution, waste %, new vs repeat | **Partly**: sales tiles exist; contribution, waste % and repeat share are not on one screen | P2 |
| Accounting export (Tally or Zoho Books) and GST return data | **Gap**: GST sales register is a Must in the plan (admin MF-7); no accounting export | P1 register, P2 export |

### 2.2 Buying and wastage: the biggest lever

| Need | State | Priority |
|---|---|---|
| Record each purchase as a batch with supplier and cost | **Have**: receipts and batches; suppliers exist | — |
| Record waste with reason | **Have**: `POST /admin/inventory/waste`, waste report | — |
| **Order cut-off for next-morning delivery** ("order by 10 pm, delivered tomorrow 7–10 am") so the morning harbour purchase matches real orders | **Gap / to decide**: slots exist; the buying rhythm is a business decision first | **P1 decision** |
| **Buying list**: tomorrow's confirmed orders summed per product and cut, plus a safety margin, before the harbour visit | **Have** (D369): admin Inventory → Buying list for a location and delivery date; confirmed orders per variant and cut, raw grams after cleaning yield, stock on hand, shortfall; safety margin typed on screen; print and CSV | — |
| Daily price update in minutes (harbour prices change every day) | **Have** (D370–D372): admin Pricing → Today's prices lists every variant with price, latest batch cost per net kg, margin and a suggested price from a target margin typed on screen; staff edit inline and save in bulk now or from tomorrow 00:00 after a dry-run review with large-change flags. Target margins are not stored yet (owner decision) | — |
| Fish age per batch; stop selling or mark down old stock | **Partly**: batches have dates; no automatic hide or end-of-day clearance price | P2 |
| Waste target and alert (e.g. above 5 % for a week) | **Gap** | P2 |
| Demand forecast from past weeks (day of week, festivals, Lent, monsoon ban) | **Gap** | P3 |
| Supplier price history and comparison | **Gap**: costs are stored per batch, no view | P3 |

Note on the **fishing ban season** (on the east coast, normally mid-April to mid-June): supply drops and prices
rise. Plan the product range and messages for it before April.

### 2.3 Operations: getting good fish to the door every day

| Need | State | Priority |
|---|---|---|
| Orders, slots, packing, shipments, riders, delivery attempts, cash settlement | **Have** | — |
| Packing slip and rider manifest | **Gap** (admin MF-1, MF-3): in the month plan | P1 |
| Daily operating rhythm written down (who does what at 10 pm, 5 am, 7 am…) | **Gap**: a one-page SOP | **P1** |
| Cold chain: ice, insulated bags, time from cleaning to door, a simple temperature check log | **Gap**: SOP plus a checkbox per batch or per day in admin later | P1 SOP, P2 log |
| FSSAI hygiene (Schedule 4): cleaning log, pest control, staff health, water | **Gap**: owner's paper or sheet checklist; FSSAI inspectors ask for it | P1 |
| **Legal Metrology**: the weighing scale must be verified and stamped, because we sell by weight; labels on pre-packed items | **Gap**: owner action. Mentioned in `docs/legal/`, not in the go-live checklist | **P1** |
| Delivery area sized to what one rider can do in a slot | **Partly**: pincodes and slot capacity exist; keep the first area small | P1 decision |
| Failed delivery handling (customer not home, refused COD) | **Have**: delivery attempts; refusal costs must be tracked (§2.5) | — |

### 2.4 Getting customers

| Need | State | Priority |
|---|---|---|
| Website found on Google for "fresh fish delivery <area>" | **Gap**: site is `noindex` until Production (by design); needs page titles, product pages indexed, sitemap after launch | P2 |
| **Google Business Profile** with photos, hours, phone, reviews | **Gap**: owner action, free, very effective for local search | **P1** |
| First-order offer | **Have**: coupons | — |
| **Referral**: "give ₹100, get ₹100" for a friend's first order | **Gap**: no referral in the system | P2 |
| Where each customer came from (Google, WhatsApp, flyer, friend) | **Gap**: no source captured; add `utm_*` on the website and a "how did you hear" choice once | P2 |
| WhatsApp channel or broadcast for today's catch (customers opt in) | **Gap**: owner process; WhatsApp Business app is free for manual broadcasts | P2 |
| Local offline: apartment societies, flyers in the delivery area, tasting days | **Gap**: owner plan | P1–P2 |

### 2.5 Keeping customers (where profit comes from)

| Need | State | Priority |
|---|---|---|
| Push notifications | **In progress** (FCM sender; app client in the month plan) | P1 |
| **"Order again"** from past orders in one tap | **Check**: order history exists; a one-tap repeat is not confirmed | P2 |
| Rating and feedback after each delivery (fish quality, rider, packing) | **Gap**: no ratings anywhere | **P2** |
| Complaint handling with photo, refund, outcome shown | **Have** mostly; outcome shown back is in the month plan | P1 |
| Weekly standing order ("every Sunday 1 kg seer fish") | **Gap**: no subscriptions; strong for repeat and for buying accuracy | P3 |
| Loyalty (points, or every 10th order free delivery) | **Gap** | P3 |
| Win-back: customers who have not ordered in 30 days get a message | **Gap** | P3 |
| Quality promise: "not fresh? we refund, no questions" | **Partly**: the complaint flow supports it; the promise needs the owner's decision and the policy text | P1 decision |

### 2.6 Payments and cash risk

| Need | State | Priority |
|---|---|---|
| Cash and UPI on delivery, cash settlement per rider | **Have** (and BE-6 fix in #71) | — |
| **Fake or refused COD orders** cost packing, fish and a trip | **Gap**: no COD limit for new customers, no block after repeated refusals | **P2** |
| Prepaid UPI (online gateway) to cut refusals and cash handling | **Partly**: adapter built and tested in test mode (D26); switch on after business registration | P2 |
| COD refunds paid by UPI or cash with a record | **In review** (#71) | P1 |
| Cash shortage per rider visible and acted on | **Have**: unsettled cash issue | — |

### 2.7 Data: seeing what is happening

| Need | State | Priority |
|---|---|---|
| App crash reports | **Have**: crash reporter in the app; needs a free GlitchTip DSN | P1 |
| Server metrics and alerts | **In progress** (OpenTelemetry, BE-8); uptime checks are owner items | P1 |
| **Funnel**: visits → product views → cart → checkout → order, per day | **Gap**: no product analytics in app or website. Use a free self-hosted tool (Umami or PostHog open source) behind an adapter, privacy-friendly (DPDP) | P2 |
| Cohorts: of customers who first ordered in week N, how many ordered again | **Gap**: can be a report from orders (no new tool) | P2 |
| Search terms with no results (what customers want and we do not sell) | **Gap** | P3 |

### 2.8 Running the software in production

| Need | State | Priority |
|---|---|---|
| Backups, restore drill, offsite copy | Scripts **have**; offsite and drill are owner and week-3 items | P1 |
| Alerts reach a person (phone), with a short on-call rule | **Gap**: who answers at 6 am when checkout fails? | P1 |
| Deploy with rollback; release notes | **Have** scripts; rehearse on the real server | P1 |
| Force app update and maintenance mode | Must in the month plan (entry MF-2) | P1 |
| Security basics: rate limits, staff 2-step sign-in, secret scanning | Rate limits **have**; staff 2FA and secret scanning to check (L-15) | P1–P2 |
| One server is a single point of failure | **Accepted at launch** (D127); revisit at ~100 orders a day | P3 |
| Cost watch: SMS, server, storage per month | **Gap**: a monthly line in the profit sheet | P2 |

### 2.9 People and support

| Need | State | Priority |
|---|---|---|
| Customer support hours, WhatsApp number, answer within N minutes in delivery hours | **Gap**: decision plus the contact page (month plan) | P1 |
| Staff roles in admin (owner, packer, rider, support) | **Have**: staff accounts and permissions | — |
| Staff training: a short guide per role using the admin | **Gap** | P1 |
| Rider pay model (per drop, per shift) that the system records | **Gap**: ties to delivery cost in §2.1 | P1 decision |

## 3. Numbers to watch every week

The owner sets the targets after the first month; these are the measures.

| Measure | Why | Where from |
|---|---|---|
| Orders per day, average order value | Volume | Reporting (have) |
| Contribution per order and % | Profit per order | Reporting, once delivery and packaging cost are recorded |
| Waste % of sales | Biggest leak | Waste report (have) |
| Repeat rate: customers ordering again within 30 days | Survival | New cohort report (P2) |
| Cost to get a customer | Is marketing paying back? | Source capture plus marketing spend (P2) |
| COD refusal % | Lost fish and trips | Delivery attempts (have data; needs a report) |
| On-time delivery % | Customer trust | Shipments and slots (have data; needs a report) |
| Complaints per 100 orders, refund ₹ | Quality | Complaints (have) |
| Rating average | Quality | Ratings (P2) |

## 4. Roadmap: six months

| Month | Theme | Main work |
|---|---|---|
| **1** (9 Nov – 6 Dec) | **Soft launch** | `team/plan-2026-11.md`, plus the P1 items of this page: delivery and packaging cost recorded, buying list, quick daily prices, SOPs (daily rhythm, cold chain, hygiene), scale stamped, Google Business Profile, support hours, rider pay model, on-call rule |
| **2** (Dec) | **Stabilise and learn** | Fix what real customers find; ratings after delivery; "order again"; funnel analytics; cohort and COD-refusal reports; COD limits for new customers; owner dashboard; accounting export; public launch when policies and Play are through |
| **3** (Jan) | **Grow in one area** | Referral; source capture and cost per customer; prepaid UPI gateway on; WhatsApp catch broadcasts; SEO; markdown of old stock; waste alert |
| **4** (Feb) | **Keep customers** | Standing weekly orders; win-back messages; loyalty (simple); search-with-no-results report |
| **5** (Mar) | **Efficiency** | Demand forecast from history; supplier price view; route order for riders; prepare for the fishing ban season (range, prices, messages) |
| **6** (Apr) | **Second area or not** | Decide from the numbers: open the next pincodes or a second store only if one area makes positive contribution and repeat rate is healthy; iOS decision |

**Rule for growth:** do not open a new area until the first one shows positive contribution per order for four
weeks in a row and waste under the target. Growth on loss-making orders only loses money faster.

## 5. Owner decisions this page needs

**Decided 2026-11-09 (owner delegated to the lead, after `research/competitors-2026-11.md`):** item 1 → D377
(next-morning delivery, 22:00 cut-off, slots 07:00–10:00 and 10:00–13:00); item 2 → D378 (₹40 per drop, packaging
₹20 per order, planning figures); item 5 → D380 (3–5 pincodes within about 4 km, one rider per slot, about 100 invited
families); target margins by category → D379. Items 3, 4, 6 and 7 are still open.

1. **Buying rhythm:** next-morning delivery with an evening cut-off, or same-day slots too?
2. **Rider pay model:** per drop, per shift or salary; this becomes the delivery cost.
3. **Quality promise:** "not fresh, full refund" or case by case?
4. **Support hours and channel** (phone, WhatsApp) and the answer time.
5. **First delivery area:** how many pincodes one rider can serve in a slot.
6. **Marketing budget** for the first two months and where it goes (flyers, societies, Google, offers).
7. Real figures for the worksheet in §1 after two weeks of trading: fish cost %, packaging, rider cost, waste.
