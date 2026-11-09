// Operations: outbox dead letters (D296, D357) and store readiness (D358). The dead-letter shape aliases the generated
// schema; the fields and the readiness report added by D357/D358 are written here until `@fresh/api-client` is
// regenerated from the OpenAPI document (CI job "OpenAPI document and API client types"), then they become aliases.

import type { Id, Instant, Schema } from "./common";

export type DeadLetter = Schema<"DeadLetterView"> & {
  /** When the event was raised (D357). */
  eventOccurredAt: Instant;
  /** The request that raised the event, to find it in the logs; null for background work (D357). */
  correlationId: string | null;
};

/** `OPEN` = not replayed yet. */
export type DeadLetterStatus = "OPEN" | "REPLAYED";

export interface DeadLetterListQuery {
  status?: DeadLetterStatus;
  page?: number;
  pageSize?: number;
}

export type ReadinessStatus = "OK" | "MISSING";

export interface ProfileFieldReadiness {
  /** `legalName`, `address`, `gstin` or `timeZone`. */
  field: string;
  status: ReadinessStatus;
}

export interface LocationReadiness {
  locationId: Id;
  cityId: Id;
  code: string;
  name: string;
  /** DRAFT, ACTIVE or PAUSED. */
  locationStatus: string;
  fssaiLicenseStatus: ReadinessStatus;
}

export interface ChannelReadiness {
  /** `SMS`, `EMAIL` or `PUSH`. */
  channel: string;
  /** `DLT`, `SMTP`, `FCM`, `DISABLED` or `DEVELOPMENT`; never a setting's value. */
  provider: string;
  status: ReadinessStatus;
}

/** `GET /admin/settings/store-readiness` (D358). */
export interface StoreReadiness {
  isReady: boolean;
  businessProfile: ProfileFieldReadiness[];
  locations: LocationReadiness[];
  channels: ChannelReadiness[];
}
