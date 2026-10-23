"use client";

import { formatDateTime } from "@fresh/core";
import {
  Button,
  Card,
  CardContent,
  EmptyState,
  ErrorState,
  Pagination,
  Skeleton,
  Switch,
  toast,
} from "@fresh/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Bell } from "lucide-react";
import { useState } from "react";
import { browserHttp } from "@/api/browser";
import {
  getPreferences,
  listNotifications,
  markAllNotificationsRead,
  markNotificationRead,
  replacePreferences,
} from "@/api/endpoints/account";
import {
  type PreferenceCategory,
  type PreferenceChannel,
  type PreferenceItem,
} from "@/api/types/account";
import { cx } from "@/lib/cx";
import { asApiError, describeError } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";

const CATEGORY_TEXT: Record<PreferenceCategory, { title: string; description: string }> = {
  ORDER_UPDATES: { title: "Order updates", description: "Confirmation, packing and delivery." },
  OFFERS: { title: "Offers", description: "Deals and new arrivals." },
};

const CHANNEL_TEXT: Record<PreferenceChannel, string> = {
  SMS: "SMS",
  EMAIL: "Email",
  PUSH: "App notifications",
  IN_APP: "In your account",
};

/** Order updates by SMS are always on (NOT-R8); the server enforces it and the switch shows it. */
function isLocked(p: PreferenceItem) {
  return p.category === "ORDER_UPDATES" && p.channel === "SMS";
}

function Preferences() {
  const queryClient = useQueryClient();
  const prefs = useQuery({
    queryKey: queryKeys.preferences,
    queryFn: () => getPreferences(browserHttp()),
  });
  const save = useMutation({
    mutationFn: (next: PreferenceItem[]) => replacePreferences(browserHttp(), next),
    onSuccess: (data) => queryClient.setQueryData(queryKeys.preferences, data),
    onError: (e) => toast({ title: describeError(e), tone: "danger" }),
  });

  if (prefs.isPending) return <Skeleton className="h-40" />;
  if (prefs.isError) {
    return (
      <ErrorState
        title="We could not load your preferences"
        description={describeError(prefs.error)}
        onRetry={() => void prefs.refetch()}
      />
    );
  }
  const items = prefs.data.preferences;
  const categories = [...new Set(items.map((p) => p.category))];

  return (
    <div className="flex flex-col gap-6">
      {categories.map((category) => (
        <fieldset key={category} className="flex flex-col gap-3">
          <legend className="text-text mb-2 font-semibold">
            {CATEGORY_TEXT[category]?.title ?? category}
            <span className="text-text-muted block text-sm font-normal">
              {CATEGORY_TEXT[category]?.description}
            </span>
          </legend>
          {items
            .filter((p) => p.category === category)
            .map((p) => (
              <Switch
                key={p.channel}
                label={CHANNEL_TEXT[p.channel] ?? p.channel}
                {...(isLocked(p)
                  ? { description: "Always on so you know where your order is." }
                  : {})}
                checked={p.enabled}
                disabled={isLocked(p) || save.isPending}
                onCheckedChange={(enabled) =>
                  save.mutate(
                    items.map((x) =>
                      x.category === p.category && x.channel === p.channel ? { ...x, enabled } : x,
                    ),
                  )
                }
              />
            ))}
        </fieldset>
      ))}
    </div>
  );
}

function Inbox() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const inbox = useQuery({
    queryKey: queryKeys.notifications(page),
    queryFn: () => listNotifications(browserHttp(), { page }),
    placeholderData: (previous) => previous,
  });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["me", "notifications"] });
  const markRead = useMutation({
    mutationFn: (id: string) => markNotificationRead(browserHttp(), id),
    onSuccess: invalidate,
  });
  const markAll = useMutation({
    mutationFn: () => markAllNotificationsRead(browserHttp()),
    onSuccess: invalidate,
    onError: (e) => toast({ title: describeError(e), tone: "danger" }),
  });

  if (inbox.isPending) return <Skeleton className="h-40" />;
  if (inbox.isError) {
    return (
      <ErrorState
        title="We could not load your messages"
        description={describeError(inbox.error)}
        traceId={asApiError(inbox.error)?.traceId}
        onRetry={() => void inbox.refetch()}
      />
    );
  }
  if (inbox.data.items.length === 0) {
    return <EmptyState icon={<Bell aria-hidden="true" />} title="No messages yet" />;
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <p className="text-text-muted text-sm">
          {inbox.data.unreadCount > 0 ? `${inbox.data.unreadCount} unread` : "All caught up"}
        </p>
        {inbox.data.unreadCount > 0 && (
          <Button
            size="sm"
            variant="ghost"
            loading={markAll.isPending}
            onClick={() => markAll.mutate()}
          >
            Mark all as read
          </Button>
        )}
      </div>
      <ul className="divide-border flex flex-col divide-y">
        {inbox.data.items.map((m) => (
          <li key={m.messageId} className="flex items-start gap-3 py-3">
            <span
              aria-hidden="true"
              className={cx(
                "mt-2 size-2 shrink-0 rounded-full",
                m.isRead ? "bg-transparent" : "bg-accent",
              )}
            />
            <div className="flex min-w-0 flex-1 flex-col gap-0.5">
              {m.title && (
                <p className={cx("text-text text-sm", !m.isRead && "font-semibold")}>{m.title}</p>
              )}
              <p className="text-text-muted text-sm">{m.body}</p>
              <p className="text-text-muted text-xs">{formatDateTime(m.createdAt)}</p>
            </div>
            {!m.isRead && (
              <Button
                size="sm"
                variant="ghost"
                onClick={() => markRead.mutate(m.messageId)}
                aria-label={`Mark "${m.title ?? "message"}" as read`}
              >
                Mark read
              </Button>
            )}
          </li>
        ))}
      </ul>
      {inbox.data.totalCount > inbox.data.pageSize && (
        <Pagination
          page={page}
          pageSize={inbox.data.pageSize}
          totalCount={inbox.data.totalCount}
          onPageChange={setPage}
        />
      )}
    </div>
  );
}

export function NotificationsView() {
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-text text-2xl font-bold">Notifications</h1>
      <Card>
        <CardContent className="flex flex-col gap-4">
          <h2 className="text-text text-lg font-semibold">Messages</h2>
          <Inbox />
        </CardContent>
      </Card>
      <Card>
        <CardContent className="flex flex-col gap-4">
          <h2 className="text-text text-lg font-semibold">How we contact you</h2>
          <Preferences />
        </CardContent>
      </Card>
    </div>
  );
}
