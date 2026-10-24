import {
  AvatarCircle,
  BentoTile,
  Button,
  CategoryBubble,
  ColourBlockScreen,
  CouponStrip,
  FilterChip,
  FloatingCartBar,
  Heading,
  OptionTile,
  PriceTag,
  ProductCard,
  QuantityStepper,
  Script,
  Sticker,
  TabBar,
} from "@fresh/ui";
import { Fish, Home, LayoutGrid, ReceiptText, ShoppingCart, Tag, UserRound } from "lucide-react";
import { type ReactNode, useState } from "react";

// Bold blocks (D88). Sample values only: in the apps every amount, count and label comes from the server
// (CLAUDE.md rule 4); the stepper here just echoes what a server would accept.

function Section({ title, note, children }: { title: string; note?: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-4">
      <div>
        <h2 className="font-display text-2xl font-extrabold tracking-[-0.025em]">{title}</h2>
        {note && <p className="text-text-muted text-sm">{note}</p>}
      </div>
      {children}
    </section>
  );
}

const tabs = [
  { href: "#home", label: "Home", icon: <Home /> },
  { href: "#shop", label: "Shop", icon: <LayoutGrid /> },
  { href: "#cart", label: "Cart", icon: <ShoppingCart />, badge: 2 },
  { href: "#orders", label: "Orders", icon: <ReceiptText /> },
  { href: "#account", label: "Account", icon: <UserRound /> },
];

function SampleCard({
  name,
  tone,
  price,
  mrp,
}: {
  name: string;
  tone: "teal" | "coral" | "mango" | "ice";
  price: number;
  mrp?: number;
}) {
  const [qty, setQty] = useState(0);
  return (
    <ProductCard
      name={name}
      label={name}
      href="#product"
      picture={<Fish className="text-accent-text size-12" strokeWidth={1.5} />}
      pictureTone={tone}
      badge="Caught 5:40 am"
      detail="500 g · 6–8 pcs"
      price={<PriceTag paise={price} mrpPaise={mrp} savingsLabel={mrp ? "13% off" : undefined} />}
      quantity={qty}
      min={1}
      max={5}
      onAdd={() => setQty(1)}
      onQuantityChange={setQty}
    />
  );
}

/** Every Bold blocks component in one place, on the canvas surface. */
export function BoldBlocksPage() {
  const [cut, setCut] = useState("STEAKS");
  const [slot, setSlot] = useState("TODAY_4");
  const [qty, setQty] = useState(1);
  const [chip, setChip] = useState(true);
  return (
    <div className="bg-canvas text-text flex flex-col gap-14 rounded-3xl p-4 sm:p-8">
      <Section
        title="Colour block screens"
        note="Coral welcome · cart · saved, teal home · tracking, mango orders · sign-in, ice location · alerts. No decorative motion."
      >
        <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-4">
          <ColourBlockScreen
            tone="coral"
            eyebrow="Your cart"
            title="Basket's"
            title2="empty"
            slot="EMPTY_CART"
            avatarFocus="50% 45%"
            headingLevel={3}
            stickers={[{ label: "Fresh today", side: "right" }]}
            sheetTitle="Nothing in here yet"
          >
            <p className="text-text-muted text-sm">
              Seer, pomfret and prawns came in this morning.
            </p>
            <Button>See today's catch</Button>
          </ColourBlockScreen>
          <ColourBlockScreen
            tone="teal"
            eyebrow="Order FO-000123"
            title="On the"
            title2="way"
            titleScript="line1"
            titleBrush="line2"
            slot="ORDER_ON_THE_WAY"
            avatarSize="sm"
            headingLevel={3}
            stickers={[{ label: "Code 4821", side: "left" }]}
            sheetTitle="12 min"
          >
            <p className="text-text-muted text-sm">Ravi is riding to you.</p>
          </ColourBlockScreen>
          <ColourBlockScreen
            tone="mango"
            eyebrow="My orders"
            title="No orders"
            title2="yet"
            slot="EMPTY_ORDERS"
            headingLevel={3}
            stickers={[{ label: "Pot's waiting", side: "right", tone: "coral" }]}
            sheetTitle="Your first fish curry is one tap away"
          >
            <Button>Start shopping</Button>
          </ColourBlockScreen>
          <ColourBlockScreen
            tone="ice"
            eyebrow="Notifications"
            title="All"
            title2="quiet"
            titleScript="line1"
            titleBrush="line2"
            slot="EMPTY_NOTIFICATIONS"
            headingLevel={3}
            sheetTitle="Nothing new"
          >
            <p className="text-text-muted text-sm">
              We'll tell you when your order is packed, on the way and delivered.
            </p>
          </ColourBlockScreen>
        </div>
      </Section>

      <Section
        title="Headings"
        note="Baloo 2 800; optional handwritten line in Dancing Script 700 (−3°, 1.12×); optional mango brush swipe; small dash doodle."
      >
        <div className="grid gap-8 sm:grid-cols-2">
          <Heading
            level={3}
            size="display"
            line1="Straight off"
            line2="the boats"
            script="line1"
            brush="line2"
            doodle
          />
          <Heading
            level={3}
            size="display"
            line1="Cleaned"
            line2="your way"
            scriptTone="accent"
            doodle
          />
          <Heading level={3} size="page">
            What&apos;s cooking <Script tone="accent">today?</Script>
          </Heading>
          <Heading level={3} size="section" line1="Bill details" />
        </div>
      </Section>

      <Section
        title="Avatar circle"
        note="Paper border, ink ring, soft shadow. xs 96 · sm 150 · md 196 · lg 240."
      >
        <div className="flex flex-wrap items-end gap-6">
          {(["xs", "sm", "md", "lg"] as const).map((size) => (
            <AvatarCircle key={size} slot="ONBOARDING_FRESH" size={size} />
          ))}
          <Sticker tone="mango" size="sm">
            Caught at dawn
          </Sticker>
          <Sticker tone="coral" size="sm" tilt="right">
            Cleaning free
          </Sticker>
          <Sticker tone="teal" size="sm">
            Iced all the way
          </Sticker>
        </div>
      </Section>

      <Section title="Bento home">
        <div className="grid max-w-xl grid-cols-2 gap-3">
          <BentoTile
            wide
            tone="teal"
            chip="Caught 5:40 am"
            title="Today's catch is in"
            description="Seer, pomfret, tiger prawns"
            slot="ONBOARDING_FRESH"
            slotFocus="55% 22%"
          />
          <BentoTile
            tone="coral"
            title="Cleaned free"
            description="Any cut"
            slot="ONBOARDING_CLEANED"
          />
          <BentoTile
            tone="mango"
            title="Same-day"
            description="Slots from 4 pm"
            slot="ONBOARDING_DELIVERY"
          />
        </div>
      </Section>

      <Section
        title="Shop: bubbles, chips, product cards"
        note="ADD sits on the picture edge and turns into a − n + stepper."
      >
        <nav aria-label="Sample categories" className="flex gap-3 overflow-x-auto pb-1">
          <CategoryBubble href="#fish" label="Fish" icon={<Fish />} selected />
          <CategoryBubble href="#prawns" label="Prawns" icon={<Fish />} tone="mango" />
          <CategoryBubble href="#crab" label="Crab" icon={<Fish />} tone="ice" />
          <CategoryBubble href="#squid" label="Squid" icon={<Fish />} tone="coral" />
        </nav>
        <div className="flex flex-wrap gap-2">
          <FilterChip pressed={chip} onClick={() => setChip((c) => !c)}>
            Cleaned free
          </FilterChip>
          <FilterChip>Under ₹500</FilterChip>
          <FilterChip>Boneless</FilterChip>
        </div>
        <div className="grid max-w-xl grid-cols-2 gap-x-3 gap-y-5">
          <SampleCard name="Seer fish steaks" tone="teal" price={59000} mrp={68000} />
          <SampleCard name="Silver pomfret" tone="coral" price={42000} />
          <SampleCard name="Tiger prawns" tone="mango" price={74900} />
          <ProductCard
            name="Mud crab"
            label="Mud crab"
            href="#crab"
            pictureTone="ice"
            picture={<Fish className="text-accent-text size-12" strokeWidth={1.5} />}
            detail="Cleaned · 1 kg"
            price={<PriceTag paise={88000} />}
            unavailableLabel="Out of stock"
          />
        </div>
      </Section>

      <Section title="Product: cut choice and stepper">
        <fieldset className="grid max-w-md grid-cols-2 gap-2">
          <legend className="font-display mb-2 text-sm font-extrabold">Choose your cut</legend>
          {[
            { value: "WHOLE", label: "Whole", description: "uncleaned weight" },
            { value: "STEAKS", label: "Steaks", description: "6–8 pcs · 500 g" },
            { value: "CURRY", label: "Curry cut", description: "bone-in pieces" },
            { value: "FILLET", label: "Fillet", description: "boneless" },
          ].map((o) => (
            <OptionTile
              key={o.value}
              name="pg-cut"
              value={o.value}
              label={o.label}
              description={o.description}
              checked={cut === o.value}
              onChange={() => setCut(o.value)}
            />
          ))}
        </fieldset>
        <div className="flex flex-wrap items-center gap-4">
          <QuantityStepper value={qty} onChange={setQty} min={1} max={5} label="Seer fish" />
          <QuantityStepper
            value={qty}
            onChange={setQty}
            min={1}
            max={5}
            label="Seer fish (small)"
            size="sm"
          />
          <QuantityStepper
            value={qty}
            onChange={setQty}
            min={1}
            max={5}
            label="Seer fish (card)"
            size="sm"
            variant="solid"
            allowRemove
          />
        </div>
      </Section>

      <Section title="Cart: coupon strip and slot buttons">
        <div className="grid max-w-md gap-3">
          <CouponStrip
            icon={<Tag />}
            title="WELCOME100 applied"
            description="You save ₹100 on this order"
          />
          <fieldset className="grid grid-cols-3 gap-2">
            <legend className="font-display mb-2 text-sm font-extrabold">Delivery slot</legend>
            {[
              { value: "TODAY_4", label: "Today", description: "4–6 pm" },
              { value: "TODAY_6", label: "Today", description: "6–8 pm" },
              { value: "TOMORROW_7", label: "Tomorrow", description: "7–9 am" },
            ].map((o) => (
              <OptionTile
                key={o.value}
                look="ink"
                name="pg-slot"
                value={o.value}
                label={o.label}
                description={o.description}
                checked={slot === o.value}
                onChange={() => setSlot(o.value)}
              />
            ))}
          </fieldset>
        </div>
      </Section>

      <Section
        title="Tab bar and cart bar"
        note="Fixed to the bottom on phones in the apps; shown in place here."
      >
        <div className="bg-background relative grid max-w-md gap-3 rounded-3xl p-3">
          <FloatingCartBar
            className="static inset-auto mx-0 animate-none"
            summary="2 items added"
            total="₹1,339"
            href="#cart"
          />
          <TabBar items={tabs} current="#home" label="Sample tabs" className="static md:grid" />
        </div>
      </Section>
    </div>
  );
}
