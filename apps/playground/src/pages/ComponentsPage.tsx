import { formatQuantity } from "@fresh/core";
import {
  Badge,
  Button,
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
  Checkbox,
  Combobox,
  Dialog,
  DialogClose,
  DialogContent,
  DialogTrigger,
  EmptyState,
  ErrorState,
  FormField,
  IconButton,
  Input,
  Pagination,
  PriceTag,
  QuantityStepper,
  RadioGroup,
  Select,
  Sheet,
  SheetContent,
  SheetTrigger,
  Skeleton,
  Spinner,
  StatusBadge,
  Switch,
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
  Textarea,
  Tooltip,
  WeightLabel,
  toast,
} from "@fresh/ui";
import { Fish, Heart, Plus, ShoppingBag, Trash2 } from "lucide-react";
import { type ReactNode, useState } from "react";
import { ORDER_STATUS } from "../status";

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{title}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">{children}</CardContent>
    </Card>
  );
}

const products = [
  { value: "seer", label: "Seer fish (Vanjaram)", keywords: ["vanjaram", "king fish"] },
  { value: "pomfret", label: "White pomfret (Vavval)", keywords: ["vavval"] },
  { value: "prawn", label: "Tiger prawns", keywords: ["eral"] },
  { value: "crab", label: "Mud crab", keywords: ["nandu"] },
];

export function ComponentsPage() {
  const [grams, setGrams] = useState(500);
  const [product, setProduct] = useState<string | null>("seer");
  const [page, setPage] = useState(3);

  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <Section title="Buttons">
        <div className="flex flex-wrap gap-2">
          <Button>Primary</Button>
          <Button variant="accent">Accent</Button>
          <Button variant="highlight">Highlight</Button>
          <Button variant="secondary">Secondary</Button>
          <Button variant="outline">Outline</Button>
          <Button variant="ghost">Ghost</Button>
          <Button variant="danger" leadingIcon={<Trash2 aria-hidden="true" />}>
            Delete
          </Button>
          <Button variant="link">Link</Button>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button size="sm">Small</Button>
          <Button size="lg" leadingIcon={<ShoppingBag aria-hidden="true" />}>
            Add to cart
          </Button>
          <Button loading>Saving</Button>
          <Button disabled>Disabled</Button>
          <IconButton label="Add to wishlist" icon={<Heart aria-hidden="true" />} />
          <IconButton label="Add item" variant="outline" icon={<Plus aria-hidden="true" />} />
          <Tooltip content="Weighed at packing; you pay for the final weight.">
            <Button variant="outline" size="sm">
              Hover or focus me
            </Button>
          </Tooltip>
        </div>
      </Section>

      <Section title="Form fields">
        <FormField label="Full name" hint="As on your ID for cash on delivery." required>
          <Input placeholder="Priya Raman" autoComplete="name" />
        </FormField>
        <FormField label="Pincode" error="We do not deliver to 600999 yet.">
          <Input inputMode="numeric" defaultValue="600999" />
        </FormField>
        <FormField label="Delivery instructions">
          <Textarea placeholder="Gate code, landmark…" />
        </FormField>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField label="Preparation">
            <Select
              defaultValue="CURRY_CUT"
              options={[
                { value: "WHOLE", label: "Whole, cleaned" },
                { value: "CURRY_CUT", label: "Curry cut" },
                { value: "FILLET", label: "Fillet" },
              ]}
            />
          </FormField>
          <FormField label="Product">
            <Combobox
              options={products}
              value={product}
              onValueChange={setProduct}
              searchPlaceholder="Search fish…"
            />
          </FormField>
        </div>
      </Section>

      <Section title="Choices">
        <Checkbox
          label="Save this address"
          description="Use it for future orders."
          defaultChecked
        />
        <Checkbox label="Disabled option" disabled />
        <RadioGroup
          label="Payment"
          defaultValue="COD"
          orientation="horizontal"
          options={[
            { value: "COD", label: "Cash on delivery" },
            { value: "UPI_ON_DELIVERY", label: "UPI on delivery" },
            { value: "ONLINE", label: "Pay online", disabled: true },
          ]}
        />
        <Switch
          label="Accept orders today"
          description="Turn off to pause new orders at this location."
          defaultChecked
        />
      </Section>

      <Section title="Badges and status">
        <div className="flex flex-wrap gap-2">
          <Badge>Neutral</Badge>
          <Badge tone="primary">Primary</Badge>
          <Badge tone="accent">Fresh catch</Badge>
          <Badge tone="highlight">Bestseller</Badge>
          <Badge tone="success">In stock</Badge>
          <Badge tone="warning">Few left</Badge>
          <Badge tone="danger">Sold out</Badge>
          <Badge tone="info">New</Badge>
        </div>
        <div className="flex flex-wrap gap-2">
          {Object.keys(ORDER_STATUS).map((code) => (
            <StatusBadge key={code} code={code} map={ORDER_STATUS} />
          ))}
          <StatusBadge code="SOMETHING_NEW" map={ORDER_STATUS} />
        </div>
      </Section>

      <Section title="Commerce">
        <PriceTag paise={64900} mrpPaise={74900} per="/ kg" savingsLabel="₹100 off" />
        <PriceTag paise={32450} prefix="Approx." size="lg" />
        <WeightLabel grams={1000} netMinGrams={700} netMaxGrams={800} prefix="Gross" />
        <QuantityStepper
          label="Seer fish"
          value={grams}
          onChange={setGrams}
          min={250}
          max={3000}
          step={250}
          formatValue={(v) => formatQuantity({ value: v, unit: "GRAM" })}
        />
        <QuantityStepper label="Crab" value={2} onChange={() => {}} pending size="sm" />
      </Section>

      <Section title="Overlays">
        <div className="flex flex-wrap gap-2">
          <Dialog>
            <DialogTrigger asChild>
              <Button variant="outline">Open dialog</Button>
            </DialogTrigger>
            <DialogContent
              title="Cancel order FO-260001?"
              description="Stock reserved for this order is released. This cannot be undone."
              footer={
                <>
                  <DialogClose asChild>
                    <Button variant="outline">Keep order</Button>
                  </DialogClose>
                  <DialogClose asChild>
                    <Button variant="danger">Cancel order</Button>
                  </DialogClose>
                </>
              }
            >
              <FormField label="Reason">
                <Textarea />
              </FormField>
            </DialogContent>
          </Dialog>
          <Sheet>
            <SheetTrigger asChild>
              <Button variant="outline">Open cart drawer</Button>
            </SheetTrigger>
            <SheetContent
              title="Your cart"
              description="2 items"
              footer={<Button fullWidth>Checkout</Button>}
            >
              <EmptyState title="Cart preview" description="Lines render here." />
            </SheetContent>
          </Sheet>
          <Sheet>
            <SheetTrigger asChild>
              <Button variant="outline">Bottom sheet</Button>
            </SheetTrigger>
            <SheetContent side="bottom" title="Choose a delivery slot">
              <p className="text-text-muted text-sm">Slots load from the server.</p>
            </SheetContent>
          </Sheet>
          <Button
            variant="secondary"
            onClick={() =>
              toast({
                title: "Added to cart",
                description: "500 g seer fish, curry cut.",
                tone: "success",
              })
            }
          >
            Success toast
          </Button>
          <Button
            variant="secondary"
            onClick={() =>
              toast({
                title: "Price changed",
                description: "Review your cart before placing the order.",
                tone: "danger",
                action: { label: "Review", onClick: () => {} },
              })
            }
          >
            Error toast
          </Button>
        </div>
        <Tabs defaultValue="items">
          <TabsList aria-label="Order detail">
            <TabsTrigger value="items">Items</TabsTrigger>
            <TabsTrigger value="timeline">Timeline</TabsTrigger>
            <TabsTrigger value="payment">Payment</TabsTrigger>
          </TabsList>
          <TabsContent value="items">3 items, 1.75 kg</TabsContent>
          <TabsContent value="timeline">Confirmed 11:00 am</TabsContent>
          <TabsContent value="payment">Cash on delivery</TabsContent>
        </Tabs>
      </Section>

      <Section title="Loading, empty and error">
        <div className="flex items-center gap-4">
          <Spinner />
          <Spinner size="lg" label="Loading orders" />
          <div className="flex flex-1 flex-col gap-2">
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-4 w-1/2" />
          </div>
        </div>
        <EmptyState
          icon={<Fish aria-hidden="true" />}
          title="No fish matches these filters"
          description="Try another category or clear filters."
          action={<Button variant="outline">Clear filters</Button>}
        />
        <ErrorState
          traceId="00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
          onRetry={() => {}}
        />
      </Section>

      <Section title="Pagination and card">
        <Pagination
          page={page}
          pageSize={20}
          totalCount={534}
          onPageChange={setPage}
          onPageSizeChange={() => {}}
        />
        <Card>
          <CardHeader>
            <CardTitle>Today at Velachery</CardTitle>
            <CardDescription>Orders to pack before the 7–9 am slot.</CardDescription>
          </CardHeader>
          <CardContent className="text-3xl font-semibold tabular-nums">42</CardContent>
          <CardFooter>
            <Button size="sm" variant="outline">
              Open pick list
            </Button>
          </CardFooter>
        </Card>
      </Section>
    </div>
  );
}
