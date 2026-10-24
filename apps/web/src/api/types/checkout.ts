// Addresses, checkout preview and place order (docs/api §4.2, §4.8), generated from the backend OpenAPI document
// (`@fresh/api-client`).

import { type Schema } from "@fresh/api-client";
import { type CartBlocker, type CartCoupon, type CartLine } from "./cart";
import { type Coded, type RequestBody } from "./common";
import { type SlotStatus } from "./locations";

export type AddressLabel = "HOME" | "WORK" | "OTHER";

export type Address = Coded<Schema<"AddressResponse">, { label: AddressLabel }>;

export type AddressInput = RequestBody<
  Coded<Schema<"SaveAddressRequest">, { label: AddressLabel }>
>;

export type PaymentMethod = Schema<"PaymentsPaymentMethod">;

export type SubstitutionPreference = "CALL_ME" | "AUTO_SIMILAR" | "NONE";

/**
 * Body of `POST /checkout/preview` and `POST /orders` (one record, `CheckoutRequest`); the preview ignores
 * `customerNotes` and `expectedTotalPaise`.
 */
export type CheckoutRequest = Coded<
  Schema<"CheckoutRequest">,
  { paymentMethod: PaymentMethod; substitutionPreference: SubstitutionPreference }
>;

export type CheckoutPreviewRequest = Omit<CheckoutRequest, "customerNotes" | "expectedTotalPaise">;

export type PlaceOrderRequest = CheckoutRequest & { expectedTotalPaise: number };

export type PaymentMethodOption = Coded<Schema<"PaymentMethodOption">, { method: PaymentMethod }>;

export type CheckoutBlocker = CartBlocker;

export type CheckoutPreview = Coded<
  Schema<"CheckoutPreview">,
  {
    address: Coded<Schema<"CheckoutAddressDto">, { label: AddressLabel | null }>;
    /** `NOT_AVAILABLE` (times null) when the slot id is unknown at this location. */
    slot: Coded<Schema<"CheckoutSlotDto">, { status: SlotStatus | "NOT_AVAILABLE" }>;
    lines: CartLine[];
    coupon: CartCoupon | null;
    paymentMethods: PaymentMethodOption[];
  }
>;
