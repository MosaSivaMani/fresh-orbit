// TEMPORARY: replace with @fresh/api-client types after Phase 3
// docs/api/README.md §4.21 (notification templates); shapes match backend
// Notifications.Application/Templates/TemplateAdminService.cs.

import type { Id, Version } from "./common";

export type NotificationChannel = "SMS" | "EMAIL" | "PUSH" | "IN_APP" | (string & {});

export interface NotificationTemplate {
  templateId: Id;
  code: string;
  channel: NotificationChannel;
  category: string | null;
  subject: string | null;
  body: string;
  isActive: boolean;
  /** Placeholders this template may use (`{{orderNumber}}` …). */
  variables: string[];
  dltTemplateId: string | null;
  version: Version;
}

export interface UpdateTemplateRequest {
  subject: string | null;
  body: string;
  isActive: boolean;
}

export interface TestSendResponse {
  /** False when the staff member has no address for the channel (PUSH / IN_APP). */
  sent: boolean;
  channel: NotificationChannel;
  recipient: string | null;
  subject: string | null;
  body: string;
}
