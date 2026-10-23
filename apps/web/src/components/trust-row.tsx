import { CalendarClock, HandCoins, Scale, Scissors } from "lucide-react";

// Store policies stated in docs/project-plan.md §1 (net-weight selling rule, cleaning to order, slots, pay on
// delivery at launch). No freshness or speed promises: those depend on live data the API reports per order.
const items = [
  {
    icon: Scissors,
    title: "Cleaned to order",
    text: "Choose whole, steaks, fillets or cleaned; we prepare it after you order.",
  },
  {
    icon: Scale,
    title: "Pay for net weight",
    text: "You pay for the weight after cleaning. The exact rule is shown on every product.",
  },
  {
    icon: CalendarClock,
    title: "Pick your slot",
    text: "Choose a delivery window that suits you at checkout.",
  },
  {
    icon: HandCoins,
    title: "Pay on delivery",
    text: "Cash or UPI when your order arrives, where available for your area.",
  },
];

export function TrustRow() {
  return (
    <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {items.map(({ icon: Icon, title, text }) => (
        <li key={title} className="bg-surface border-border flex gap-3 rounded-xl border p-5">
          <span className="bg-accent-subtle text-accent-text flex size-10 shrink-0 items-center justify-center rounded-full">
            <Icon aria-hidden="true" className="size-5" />
          </span>
          <div>
            <p className="text-text font-semibold">{title}</p>
            <p className="text-text-muted text-sm">{text}</p>
          </div>
        </li>
      ))}
    </ul>
  );
}
