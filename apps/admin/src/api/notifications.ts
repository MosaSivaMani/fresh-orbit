import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useHttp } from "../app/services";
import { type Version } from "./types/common";
import {
  type NotificationTemplate,
  type TestSendResponse,
  type UpdateTemplateRequest,
} from "./types/notifications";

// docs/api/README.md §4.21 — notification templates (notifications.manage).

export const notificationKeys = {
  templates: ["notifications", "templates"] as const,
};

export function useNotificationTemplates() {
  const http = useHttp();
  return useQuery({
    queryKey: notificationKeys.templates,
    queryFn: ({ signal }) =>
      http.get<NotificationTemplate[]>("/admin/notification-templates", { signal }),
  });
}

function templatePath(code: string, channel: string): string {
  return `/admin/notification-templates/${encodeURIComponent(code)}/${encodeURIComponent(channel)}`;
}

export function useUpdateTemplate() {
  const http = useHttp();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      code,
      channel,
      body,
      version,
    }: {
      code: string;
      channel: string;
      body: UpdateTemplateRequest;
      version: Version;
    }) => http.put<NotificationTemplate>(templatePath(code, channel), body, { ifMatch: version }),
    onSuccess: () => qc.invalidateQueries({ queryKey: notificationKeys.templates }),
  });
}

/** Sends the template to the signed-in staff member's own contact. */
export function useSendTestTemplate() {
  const http = useHttp();
  return useMutation({
    mutationFn: ({ code, channel }: { code: string; channel: string }) =>
      http.post<TestSendResponse>(`${templatePath(code, channel)}/test`),
  });
}
