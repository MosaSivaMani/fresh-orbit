// TEMPORARY: replace with @fresh/api-client types after Phase 3
// Written from docs/api/README.md §4.2, §4.7, §4.10, docs/product/customers.md and docs/product/notifications.md;
// field names follow the Phase 2 Customers / Notifications contracts where the docs are silent.

import { type ProductListItem } from "./catalog";

export interface Profile {
  customerId: string;
  phone: string | null;
  fullName: string | null;
  email: string | null;
  marketingOptIn: boolean;
  status: "ACTIVE" | "DELETED";
  version: string;
}

/** `PATCH /me`: null leaves a field unchanged (CUS-R2). */
export interface UpdateProfileRequest {
  fullName?: string | null;
  email?: string | null;
  marketingOptIn?: boolean | null;
}

export interface WishlistItem {
  productId: string;
  addedAt: string;
  /**
   * docs/api §4.2 lists "wishlist products (priced when LOC given)"; Phase 2 returns ids only. The website shows the
   * product card when the server sends it (see docs/design/web.md "API gaps").
   */
  product?: ProductListItem | null;
}

export interface InboxItem {
  messageId: string;
  templateCode: string;
  title: string | null;
  body: string;
  isRead: boolean;
  createdAt: string;
  readAt: string | null;
}

export interface InboxResponse {
  items: InboxItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  unreadCount: number;
}

export type PreferenceCategory = "ORDER_UPDATES" | "OFFERS";
export type PreferenceChannel = "SMS" | "EMAIL" | "PUSH" | "IN_APP";

export interface PreferenceItem {
  category: PreferenceCategory;
  channel: PreferenceChannel;
  enabled: boolean;
}

export interface PreferencesResponse {
  preferences: PreferenceItem[];
}

export interface VisibleCoupon {
  code: string;
  description: string | null;
  minOrderPaise: number | null;
  validTo: string | null;
}
