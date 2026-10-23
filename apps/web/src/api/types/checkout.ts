// TEMPORARY: replace with @fresh/api-client types after Phase 3
// Written from docs/api/README.md §4.2 (addresses), §4.8 (preview, place order) and docs/product/ordering.md,
// docs/product/customers.md (CUS-R5 address fields).

import { type CartCoupon, type CartLine, type CartTotals } from "./cart";
import { type Issue } from "./common";
import { type SlotStatus } from "./locations";

export type AddressLabel = "HOME" | "WORK" | "OTHER";

export interface Address {
  /** Phase 2 Customers returns `addressId`; docs/api examples use `id`. Use {@link addressIdOf}. */
  addressId?: string;
  id?: string;
  label: AddressLabel;
  recipientName: string;
  phone: string;
  line1: string;
  line2: string | null;
  landmark: string | null;
  locality: string;
  cityName: string;
  stateCode: string;
  pincode: string;
  latitude?: number | null;
  longitude?: number | null;
  isDefault: boolean;
  /** Null until Customers can ask Locations (CUS-R5). */
  isServiceable: boolean | null;
  version: string;
}

export function addressIdOf(address: Pick<Address, "addressId" | "id">): string {
  return address.addressId ?? address.id ?? "";
}

export interface AddressInput {
  label: AddressLabel;
  recipientName: string;
  phone: string;
  line1: string;
  line2: string | null;
  landmark: string | null;
  locality: string;
  cityName: string;
  stateCode: string;
  pincode: string;
  isDefault?: boolean | null;
}

export type PaymentMethod = "COD" | "UPI_ON_DELIVERY" | "ONLINE";

export type SubstitutionPreference = "CALL_ME" | "AUTO_SIMILAR" | "NONE";

export interface CheckoutPreviewRequest {
  addressId: string;
  deliverySlotId: string;
  paymentMethod: PaymentMethod;
  substitutionPreference: SubstitutionPreference;
}

export interface PaymentMethodOption {
  method: PaymentMethod;
  enabled: boolean;
  reasonCode: string | null;
}

export interface CheckoutBlocker extends Issue {
  lineId?: string | null;
}

export interface CheckoutPreview {
  cartId: string;
  address: {
    id: string;
    label: AddressLabel;
    line1: string;
    locality: string;
    pincode: string;
    isServiceable: boolean;
  };
  fulfillmentLocationCode: string;
  slot: { id: string; startAt: string; endAt: string; cutoffAt: string; status: SlotStatus } | null;
  lines: CartLine[];
  coupon: CartCoupon | null;
  totals: CartTotals;
  paymentMethods: PaymentMethodOption[];
  canPlaceOrder: boolean;
  blockers: CheckoutBlocker[];
  /** Send back unchanged as `expectedTotalPaise` when placing the order. */
  expectedTotalPaise: number;
}

export interface PlaceOrderRequest extends CheckoutPreviewRequest {
  customerNotes: string | null;
  expectedTotalPaise: number;
}
