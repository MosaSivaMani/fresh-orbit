"use client";

import { Button, toast } from "@fresh/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { BellRing } from "lucide-react";
import Link from "next/link";
import { browserHttp } from "@/api/browser";
import { createStockAlert, deleteStockAlert, listStockAlerts } from "@/api/endpoints/account";
import { getServiceability } from "@/api/endpoints/catalog";
import { useSession } from "@/auth/use-session";
import { describeError } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";

/**
 * "Notify me" for a sold-out size (`/me/stock-alerts`). The alert is for the store that serves the visitor's pincode,
 * taken from `GET /serviceability`. Guests are sent to sign in and come back here.
 */
export function NotifyMe({
  variantId,
  pincode,
  returnTo,
}: {
  variantId: string;
  pincode: string;
  returnTo: string;
}) {
  const queryClient = useQueryClient();
  const { status } = useSession();
  const signedIn = status === "signedIn";
  const alerts = useQuery({
    queryKey: queryKeys.stockAlerts,
    queryFn: () => listStockAlerts(browserHttp()),
    enabled: signedIn,
  });
  const existing = alerts.data?.find((a) => a.variantId === variantId);

  const create = useMutation({
    mutationFn: async () => {
      const area = await getServiceability(browserHttp(), pincode);
      if (!area.fulfillmentLocation) throw new Error("We do not deliver to this pincode yet.");
      return createStockAlert(browserHttp(), variantId, area.fulfillmentLocation.id);
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.stockAlerts });
      toast({ title: "We will tell you when it is back", tone: "success" });
    },
    onError: (e) => toast({ title: describeError(e), tone: "danger" }),
  });
  const remove = useMutation({
    mutationFn: (alertId: string) => deleteStockAlert(browserHttp(), alertId),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.stockAlerts }),
    onError: (e) => toast({ title: describeError(e), tone: "danger" }),
  });

  if (!signedIn) {
    return (
      <Button asChild variant="outline">
        <Link href={`/login?next=${encodeURIComponent(returnTo)}`}>
          <BellRing aria-hidden="true" />
          Sign in to get notified
        </Link>
      </Button>
    );
  }
  if (existing) {
    return (
      <div className="bg-teal-soft flex flex-wrap items-center justify-between gap-2 rounded-[20px] p-3 text-sm">
        <p className="text-text font-semibold">We will tell you when this size is back.</p>
        <Button
          size="sm"
          variant="ghost"
          loading={remove.isPending}
          onClick={() => remove.mutate(existing.id)}
        >
          Stop alert
        </Button>
      </div>
    );
  }
  return (
    <Button
      variant="outline"
      leadingIcon={<BellRing aria-hidden="true" />}
      loading={create.isPending || alerts.isPending}
      onClick={() => create.mutate()}
    >
      Notify me when it is back
    </Button>
  );
}
