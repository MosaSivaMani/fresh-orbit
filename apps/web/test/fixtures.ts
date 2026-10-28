// Test fixtures typed by the generated API schema (`@fresh/api-client`). Values are whatever the "server" says; tests assert that the UI
// renders them, never that it computes them.
import { type ProductDetail } from "@/api/types/catalog";
import { type Cart, type CartLine } from "@/api/types/cart";
import { type Address, type CheckoutPreview } from "@/api/types/checkout";
import { type ServiceabilityResponse } from "@/api/types/locations";

export const serviceable: ServiceabilityResponse = {
  pincode: "600041",
  serviceable: true,
  reasonCode: null,
  city: {
    id: "c1",
    code: "CHENNAI",
    name: "Chennai",
    timeZone: "Asia/Kolkata",
    stateCode: "TN",
    stateName: "Tamil Nadu",
  },
  fulfillmentLocation: {
    id: "l1",
    code: "CHN-TVM",
    name: "Thiruvanmiyur Store",
    hasColdStorage: true,
  },
  serviceArea: {
    id: "sa1",
    name: "Adyar - Thiruvanmiyur",
    deliveryFeePaise: 3900,
    freeDeliveryAbovePaise: 49900,
    minOrderPaise: 29900,
  },
  nextSlot: { id: "s1", startAt: "2026-10-23T08:30:00Z", endAt: "2026-10-23T10:30:00Z" },
};

export const notServiceable: ServiceabilityResponse = {
  pincode: "600123",
  serviceable: false,
  reasonCode: "PINCODE_NOT_SERVICEABLE",
  city: null,
  fulfillmentLocation: null,
  serviceArea: null,
  nextSlot: null,
};

export const seerFish: ProductDetail = {
  id: "p1",
  slug: "seer-fish-vanjaram",
  name: "Seer Fish",
  localNames: ["Vanjaram", "Neymeen", "Surmai"],
  speciesScientificName: "Scomberomorus commerson",
  description: "Firm, mildly sweet flesh; ideal for fry and curry.",
  sourceType: "WILD",
  sourceRegion: "Kasimedu, Chennai",
  storageInstructions: "Keep at 0-4 °C; cook within 24 hours.",
  category: { slug: "fish", name: "Fish" },
  images: [
    {
      url: "https://img.test/seer-1280.webp",
      altText: "Seer fish steaks",
      width: 1280,
      height: 960,
      sources: [],
    },
  ],
  location: { fulfillmentLocationCode: "CHN-TVM", pincode: "600041" },
  variants: [
    {
      id: "v1",
      sku: "SEER-KG",
      name: "Medium (3-5 kg fish)",
      sellUnit: "KG",
      isWeightVariable: true,
      packSizeGrams: null,
      avgPieceGrams: null,
      minOrder: { value: 500, unit: "GRAM" },
      maxOrder: { value: 5000, unit: "GRAM" },
      orderStep: { value: 250, unit: "GRAM" },
      unitPricePaise: 129900,
      compareAtPaise: 149900,
      preparations: [
        {
          code: "WHOLE",
          name: "Whole, uncleaned",
          yieldBp: 10000,
          isDefault: false,
          chargePaise: 0,
          chargeBasis: "FLAT",
        },
        {
          code: "STEAK",
          name: "Steaks (1 cm)",
          yieldBp: 7000,
          isDefault: true,
          chargePaise: 4000,
          chargeBasis: "PER_KG",
        },
      ],
      availability: {
        status: "IN_STOCK",
        maxOrderableNow: { value: 5000, unit: "GRAM" },
        message: null,
      },
    },
  ],
  netWeightNote:
    "You pay for net weight after cleaning. Final weight may vary up to 10% above the ordered weight.",
  availability: { status: "IN_STOCK", message: null },
  isWishlisted: false,
  locationRequired: false,
};

export const seerLine: CartLine = {
  id: "line1",
  productId: "p1",
  productSlug: "seer-fish-vanjaram",
  productName: "Seer Fish (Vanjaram)",
  variantId: "v1",
  variantName: "Medium (3-5 kg fish)",
  sellUnit: "KG",
  imageUrl: null,
  preparation: { code: "STEAK", name: "Steaks (1 cm)", yieldBp: 7000 },
  quantity: { value: 500, unit: "GRAM" },
  estimatedNetQuantity: { value: 500, unit: "GRAM" },
  maxChargeableQuantity: { value: 550, unit: "GRAM" },
  unitPricePaise: 129900,
  compareAtPaise: 149900,
  priceUnit: "KG",
  itemPaise: 64950,
  preparationChargePaise: 2000,
  preparationChargeBasis: "PER_KG",
  taxRateBp: 0,
  taxPaise: 0,
  estimatedLineTotalPaise: 66950,
  maxLineTotalPaise: 73645,
  availability: { status: "IN_STOCK", message: null },
  isSellable: true,
  issues: [],
  minOrder: { value: 500, unit: "GRAM" },
  maxOrder: { value: 5000, unit: "GRAM" },
  orderStep: { value: 250, unit: "GRAM" },
};

export const emptyCart: Cart = {
  id: null,
  cartToken: null,
  pincode: null,
  fulfillmentLocationCode: null,
  lines: [],
  coupon: null,
  totals: {
    itemsPaise: 0,
    preparationPaise: 0,
    subtotalPaise: 0,
    discountPaise: 0,
    deliveryFeePaise: 0,
    deliveryFeeWaived: false,
    taxPaise: 0,
    estimatedTotalPaise: 0,
    maxTotalPaise: 0,
    minOrderPaise: 0,
    minOrderShortfallPaise: 0,
    freeDeliveryShortfallPaise: null,
  },
  canCheckout: false,
  checkoutBlockers: [{ code: "CART_EMPTY", lineId: null, message: "Your cart is empty." }],
  messages: [],
};

export const cartWithSeer: Cart = {
  id: "cart1",
  cartToken: null,
  pincode: "600041",
  fulfillmentLocationCode: "CHN-TVM",
  lines: [seerLine],
  coupon: null,
  totals: {
    itemsPaise: 64950,
    preparationPaise: 2000,
    subtotalPaise: 66950,
    discountPaise: 0,
    deliveryFeePaise: 0,
    deliveryFeeWaived: true,
    taxPaise: 0,
    estimatedTotalPaise: 66950,
    maxTotalPaise: 73645,
    minOrderPaise: 29900,
    minOrderShortfallPaise: 0,
    freeDeliveryShortfallPaise: 0,
  },
  canCheckout: true,
  checkoutBlockers: [],
  messages: [
    "Weight-based items are charged on actual net weight, up to 10% over the ordered weight.",
  ],
};

export const homeAddress: Address = {
  addressId: "a1",
  label: "HOME",
  recipientName: "Priya Raman",
  phone: "+919876543210",
  line1: "12, 3rd Cross Street",
  line2: null,
  landmark: "Near RTO",
  locality: "Thiruvanmiyur",
  cityName: "Chennai",
  stateCode: "TN",
  pincode: "600041",
  latitude: null,
  longitude: null,
  isDefault: true,
  isServiceable: true,
  version: "11",
};

export function preview(expectedTotalPaise: number): CheckoutPreview {
  return {
    cartId: "cart1",
    address: {
      id: "a1",
      label: "HOME",
      line1: "12, 3rd Cross Street",
      locality: "Thiruvanmiyur",
      pincode: "600041",
      isServiceable: true,
    },
    fulfillmentLocationCode: "CHN-TVM",
    slot: {
      id: "slot1",
      startAt: "2026-10-23T08:30:00Z",
      endAt: "2026-10-23T10:30:00Z",
      cutoffAt: "2026-10-23T06:30:00Z",
      status: "AVAILABLE",
    },
    lines: [seerLine],
    coupon: null,
    totals: {
      ...cartWithSeer.totals,
      estimatedTotalPaise: expectedTotalPaise,
    },
    paymentMethods: [
      { method: "COD", enabled: true, reasonCode: null },
      { method: "UPI_ON_DELIVERY", enabled: true, reasonCode: null },
      { method: "ONLINE", enabled: false, reasonCode: "PAYMENT_METHOD_NOT_ALLOWED" },
    ],
    canPlaceOrder: true,
    blockers: [],
    expectedTotalPaise,
  };
}

export function problem(status: number, code: string, extra: Record<string, unknown> = {}) {
  return {
    type: `https://api.freshorbit.in/problems/${code.toLowerCase()}`,
    title: "Refused.",
    status,
    code,
    traceId: "00-test-trace-01",
    errors: {},
    ...extra,
  };
}
