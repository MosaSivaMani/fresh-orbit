// Audit log (docs/api §4.21), generated from the backend OpenAPI document.

import type { CalendarDate, Id, Schema } from "./common";

/** `before` / `after` are JSON snapshots (null for creates / deletes); the spec types them as `JsonElement`. */
export type AuditEntry = Omit<Schema<"AuditEntryResponse">, "before" | "after"> & {
  before: unknown;
  after: unknown;
};

export interface AuditListQuery {
  entityType?: string;
  entityId?: string;
  actorId?: Id;
  action?: string;
  fromDate?: CalendarDate;
  toDate?: CalendarDate;
  page?: number;
  pageSize?: number;
}
