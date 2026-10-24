// Notification templates (docs/api §4.21), generated from the backend OpenAPI document.

import type { Schema } from "./common";

export type NotificationChannel = "SMS" | "EMAIL" | "PUSH" | "IN_APP" | (string & {});

export type NotificationTemplate = Schema<"TemplateResponse">;

export type UpdateTemplateRequest = Schema<"UpdateTemplateRequest">;

/** `sent` is false when the staff member has no address for the channel (PUSH / IN_APP). */
export type TestSendResponse = Schema<"TestSendResponse">;
