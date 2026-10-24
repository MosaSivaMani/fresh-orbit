import { illustrationSlots, illustrations } from "@fresh/illustrations";
import {
  Badge,
  Button,
  EmptyState,
  FeatureCard,
  HelpHero,
  HeroBanner,
  Illustration,
  PolaroidCard,
  ReceiptCard,
  Sticker,
} from "@fresh/ui";
import { MessageCircle, Phone } from "lucide-react";
import type { ReactNode } from "react";

// Sample values only: in the apps every amount comes formatted from the server (CLAUDE.md rule 4).
const receiptSections = [
  [
    {
      id: "seer",
      label: "Seer fish (vanjaram)",
      description: "500 g, curry cut",
      value: "₹649.00",
    },
    { id: "prawn", label: "Tiger prawns", description: "250 g, cleaned", value: "₹380.00" },
  ],
  [
    { id: "coupon", label: "Coupon FRESH50", value: "−₹50.00", tone: "saving" as const },
    { id: "delivery", label: "Delivery", value: "Free", tone: "muted" as const },
  ],
];

function Section({ title, note, children }: { title: string; note?: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-4">
      <div>
        <h2 className="font-display text-2xl font-bold">{title}</h2>
        {note && <p className="text-text-muted text-sm">{note}</p>}
      </div>
      {children}
    </section>
  );
}

/** Mock pages and every illustrated component (D53), on the sand surface. */
export function IllustratedPage() {
  return (
    <div className="bg-surface-sand text-text flex flex-col gap-14 rounded-3xl p-4 sm:p-8">
      <Section title="Home page (mock)">
        <HeroBanner
          title="Fresh from the boats, at your door today"
          description="Cleaned and cut the way you cook, weighed honestly and kept on ice all the way."
          actions={
            <>
              <Button size="lg">Shop today&apos;s catch</Button>
              <Button size="lg" variant="outline">
                Choose your area
              </Button>
            </>
          }
          stickers={[
            { label: "Caught today", corner: "top-left" },
            { label: "On ice", tone: "mango", corner: "bottom-right" },
          ]}
        />
        <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4">
          <FeatureCard
            slot="FEATURE_HONEST_WEIGHT"
            title="Honest weight"
            description="Weighed after cleaning. You pay for what you get."
          />
          <FeatureCard
            slot="FEATURE_CLEANED_YOUR_WAY"
            tone="coral"
            title="Cleaned your way"
            description="Curry cut, fry cut or whole: you choose."
          />
          <FeatureCard
            slot="FEATURE_COLD_CHAIN"
            tone="ice"
            title="Cold all the way"
            description="Packed on ice from the harbour to your door."
          />
          <FeatureCard
            slot="FEATURE_SAME_DAY"
            tone="teal"
            title="Same-day delivery"
            description="Order by noon, cook it tonight."
          />
        </div>
        <div className="grid items-start gap-12 py-4 lg:grid-cols-[1.2fr_1fr]">
          <div className="flex justify-center px-4">
            <PolaroidCard
              slot="ONBOARDING_FRESH"
              caption="Padma picks every fish at dawn"
              stickers={[
                { label: "Fresh today", corner: "top-left" },
                { label: "Honest weight", tone: "teal", corner: "top-right" },
                { label: "Same day", tone: "mango", corner: "bottom-left" },
              ]}
            />
          </div>
          <ReceiptCard
            className="mx-auto w-full max-w-sm"
            title="Your order"
            meta="Packed at the harbour store, 7:40 am"
            sections={receiptSections}
            total={{ label: "Total", value: "₹979.00" }}
            note="Weighed 12 g over, well under the 10% cap. You are not charged for it."
          />
        </div>
      </Section>

      <Section
        title="Scene images"
        note="Final images with a full background (SCENE) are cropped with object-fit: cover in a rounded frame."
      >
        <HeroBanner
          slot="ONBOARDING_CLEANED"
          headingLevel={3}
          reverse
          title="Cleaned the way you cook"
          description="Tell us curry cut or fry cut; Padma does the rest."
          actions={<Button>See cuts</Button>}
        />
        <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4">
          <FeatureCard
            slot="ONBOARDING_FRESH"
            title="Straight from the harbour"
            description="Picked at sunrise."
          />
          <FeatureCard
            slot="ONBOARDING_CLEANED"
            title="Cut to order"
            description="Steaks, curry cut or whole."
          />
          <div className="flex flex-col items-start gap-2">
            <Illustration slot="ONBOARDING_FRESH" size="md" />
            <p className="text-text-muted text-xs">Illustration size md (auto fit)</p>
          </div>
          <div className="flex flex-col items-start gap-2">
            <Illustration slot="ONBOARDING_CLEANED" size="sm" />
            <p className="text-text-muted text-xs">Illustration size sm (auto fit)</p>
          </div>
        </div>
      </Section>

      <Section title="Empty cart and help">
        <div className="grid gap-6 lg:grid-cols-2">
          <div className="bg-surface-paper shadow-soft-md rounded-[28px] p-6">
            <EmptyState
              slot="EMPTY_CART"
              headingLevel={3}
              title="Your cart is empty"
              description="Today's catch is waiting: seer fish, prawns and more."
              action={<Button>Browse fish</Button>}
            />
          </div>
          <div className="bg-surface-paper shadow-soft-md rounded-[28px] p-6">
            <HelpHero
              headingLevel={3}
              title="How can we help you today?"
              description="Anil and the team answer within a few minutes."
              actions={
                <>
                  <Button variant="accent">
                    <MessageCircle aria-hidden="true" />
                    Chat with us
                  </Button>
                  <Button variant="outline">
                    <Phone aria-hidden="true" />
                    Call us
                  </Button>
                </>
              }
            />
          </div>
        </div>
      </Section>

      <Section title="Stickers and buttons">
        <div className="flex flex-wrap items-center gap-5">
          <Sticker>Fresh today</Sticker>
          <Sticker tone="mango" tilt="right">
            Bestseller
          </Sticker>
          <Sticker tone="teal">Honest weight</Sticker>
          <Sticker tone="paper" tilt="none">
            Same day
          </Sticker>
          <Sticker size="sm">New</Sticker>
          <Button variant="sticker">Order now</Button>
          <Button>Primary</Button>
          <Button variant="accent">Accent</Button>
          <Button variant="outline">Outline</Button>
        </div>
      </Section>

      <Section
        title={`All slots (${illustrationSlots.length})`}
        note="Placeholders are our own clay-look SVGs until the owner's final image is imported."
      >
        <ul className="grid list-none grid-cols-[repeat(auto-fill,minmax(200px,1fr))] gap-4 p-0">
          {illustrationSlots.map((slot) => {
            const entry = illustrations[slot];
            return (
              <li
                key={slot}
                className="bg-surface-paper shadow-soft-sm flex flex-col gap-2 rounded-3xl p-3"
              >
                <div className="bg-surface-sand-strong flex aspect-[4/3] items-center justify-center overflow-hidden rounded-2xl">
                  <Illustration slot={slot} size="fill" decorative className="size-full" />
                </div>
                <div className="flex flex-wrap items-center gap-1.5">
                  <code className="text-xs font-semibold">{slot}</code>
                  <Badge tone={entry.status === "FINAL" ? "success" : "neutral"}>
                    {entry.status}
                  </Badge>
                  <Badge tone="info">{entry.background}</Badge>
                </div>
                <p className="text-text-muted text-xs">{entry.alt}</p>
              </li>
            );
          })}
        </ul>
      </Section>
    </div>
  );
}
