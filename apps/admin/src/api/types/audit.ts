// TEMPORARY: replace with @fresh/api-client types after Phase 3
// docs/api/README.md §4.21 (audit log); shapes match backend Audit.Application/Entries/AuditQueries.cs.

import type { CalendarDate, Id, Instant } from "./common";

export interface AuditEntry {
  id: number;
  occurredAt: Instant;
  actorType: "CUSTOMER" | "STAFF" | "SYSTEM" | (string & {});
  actorId: Id | null;
  actorName: string | null;
  action: string;
  entityType: string;
  entityId: string;
  /** JSON snapshot before the change (null for creates). */
  before: unknown;
  /** JSON snapshot after the change (null for deletes). */
  after: unknown;
  ip: string | null;
  correlationId: string | null;
}

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
