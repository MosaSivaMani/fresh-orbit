import { formatQuantity, formatTimeRange } from "@fresh/core";
import {
  Badge,
  Button,
  Card,
  FormField,
  IconButton,
  PriceTag,
  QuantityStepper,
  Select,
  WeightLabel,
  toast,
} from "@fresh/ui";
import { Clock, Heart, ShieldCheck, ShoppingBag, Snowflake } from "lucide-react";
import { useState } from "react";

/** Simple, brand-coloured illustration so the playground needs no image assets. */
function FishArt() {
  return (
    <svg
      viewBox="0 0 320 180"
      role="img"
      aria-label="Seer fish illustration"
      className="h-full w-full"
    >
      <rect width="320" height="180" fill="var(--fo-color-accent-subtle)" />
      <path
        d="M60 90 C110 40, 200 40, 240 90 C200 140, 110 140, 60 90 Z"
        fill="var(--fo-brand-ocean-teal)"
        opacity="0.9"
      />
      <path d="M240 90 L285 60 L275 90 L285 120 Z" fill="var(--fo-brand-sea-green)" />
      <circle cx="95" cy="82" r="6" fill="var(--fo-brand-soft-background)" />
      <path
        d="M130 70 C150 85, 150 95, 130 110"
        stroke="var(--fo-brand-warm-sand)"
        strokeWidth="4"
        fill="none"
      />
    </svg>
  );
}

const products = [
  {
    id: "seer",
    name: "Seer fish (Vanjaram)",
    tagline: "Line-caught off Kasimedu",
    pricePaise: 129900,
    mrpPaise: 149900,
    netMinGrams: 700,
    netMaxGrams: 800,
    badges: [
      { label: "Fresh catch", tone: "accent" as const },
      { label: "Bestseller", tone: "highlight" as const },
    ],
  },
  {
    id: "pomfret",
    name: "White pomfret (Vavval)",
    tagline: "Whole, 3–4 pieces per kg",
    pricePaise: 99900,
    mrpPaise: null,
    netMinGrams: 750,
    netMaxGrams: 850,
    badges: [{ label: "Few left", tone: "warning" as const }],
  },
];

function ProductCard({ product }: { product: (typeof products)[number] }) {
  const [grams, setGrams] = useState(500);
  const [prep, setPrep] = useState("CURRY_CUT");
  const [saved, setSaved] = useState(false);

  return (
    <Card className="flex flex-col overflow-hidden">
      <div className="relative aspect-[16/9]">
        <FishArt />
        <div className="absolute top-3 left-3 flex gap-1.5">
          {product.badges.map((b) => (
            <Badge key={b.label} tone={b.tone}>
              {b.label}
            </Badge>
          ))}
        </div>
        <IconButton
          label={
            saved ? `Remove ${product.name} from wishlist` : `Save ${product.name} to wishlist`
          }
          aria-pressed={saved}
          onClick={() => setSaved((s) => !s)}
          className="bg-surface/90 hover:bg-surface absolute top-2 right-2"
          icon={
            <Heart aria-hidden="true" className={saved ? "fill-danger text-danger" : undefined} />
          }
        />
      </div>
      <div className="flex flex-1 flex-col gap-3 p-4">
        <div>
          <h3 className="font-display text-lg leading-6 font-semibold">{product.name}</h3>
          <p className="text-text-muted text-sm">{product.tagline}</p>
        </div>
        <PriceTag paise={product.pricePaise} mrpPaise={product.mrpPaise} per="/ kg" />
        <WeightLabel
          grams={1000}
          netMinGrams={product.netMinGrams}
          netMaxGrams={product.netMaxGrams}
          prefix="Per"
        />
        <FormField label="Preparation">
          <Select
            value={prep}
            onValueChange={setPrep}
            options={[
              { value: "WHOLE", label: "Whole, cleaned" },
              { value: "CURRY_CUT", label: "Curry cut" },
              { value: "STEAKS", label: "Steaks" },
            ]}
          />
        </FormField>
        <div className="flex items-center justify-between gap-3">
          <QuantityStepper
            label={product.name}
            value={grams}
            onChange={setGrams}
            min={250}
            max={3000}
            step={250}
            formatValue={(v) => formatQuantity({ value: v, unit: "GRAM" })}
            size="sm"
          />
          <Button
            variant="primary"
            leadingIcon={<ShoppingBag aria-hidden="true" />}
            onClick={() =>
              toast({
                title: "Added to cart",
                description: `${formatQuantity({ value: grams, unit: "GRAM" })} ${product.name}`,
                tone: "success",
              })
            }
          >
            Add
          </Button>
        </div>
        <ul className="border-border text-text-muted mt-auto flex flex-col gap-1.5 border-t pt-3 text-xs">
          <li className="flex items-center gap-2">
            <Clock aria-hidden="true" className="text-accent size-3.5" />
            Next slot today {formatTimeRange("17:00", "19:00")}
          </li>
          <li className="flex items-center gap-2">
            <Snowflake aria-hidden="true" className="text-accent size-3.5" />
            Packed on ice, delivered chilled
          </li>
          <li className="flex items-center gap-2">
            <ShieldCheck aria-hidden="true" className="text-accent size-3.5" />
            Pay for final weight; never more than shown
          </li>
        </ul>
      </div>
    </Card>
  );
}

export function ProductPage() {
  return (
    <div className="flex flex-col gap-4">
      <p className="text-text-muted max-w-prose text-sm">
        Sample customer product cards. Prices and net weights here are placeholders; in the apps
        they come from the server and are only formatted on the client.
      </p>
      <div className="grid gap-4 sm:grid-cols-2">
        {products.map((p) => (
          <ProductCard key={p.id} product={p} />
        ))}
      </div>
    </div>
  );
}
