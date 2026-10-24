"use client";

import { Badge, Button, Dialog, DialogContent, Skeleton, toast } from "@fresh/ui";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { MapPin, MapPinPlus } from "lucide-react";
import { useState } from "react";
import { browserHttp } from "@/api/browser";
import {
  addAddress,
  deleteAddress,
  listAddresses,
  makeDefaultAddress,
  replaceAddress,
} from "@/api/endpoints/account";
import { type Address } from "@/api/types/checkout";
import { EmptyPage, LoadError } from "@/components/states";
import { describeError, hasCode } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";
import { AddressForm } from "./address-form";

const LABEL_TEXT: Record<string, string> = { HOME: "Home", WORK: "Work", OTHER: "Other" };

type Editing = { kind: "new" } | { kind: "edit"; address: Address } | null;

export function AddressesView() {
  const queryClient = useQueryClient();
  const addresses = useQuery({
    queryKey: queryKeys.addresses,
    queryFn: () => listAddresses(browserHttp()),
  });
  const [editing, setEditing] = useState<Editing>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const refresh = () => queryClient.invalidateQueries({ queryKey: queryKeys.addresses });

  async function run(id: string, work: () => Promise<unknown>, done: string) {
    setBusyId(id);
    try {
      await work();
      await refresh();
      toast({ title: done, tone: "success" });
    } catch (e) {
      toast({ title: describeError(e), tone: "danger" });
    } finally {
      setBusyId(null);
    }
  }

  if (addresses.isPending) return <Skeleton className="h-60 rounded-[22px]" />;
  if (addresses.isError) {
    return (
      <LoadError
        title="We could not load your addresses"
        error={addresses.error}
        onRetry={() => void addresses.refetch()}
      />
    );
  }

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-text text-3xl font-extrabold">Addresses</h1>
        <Button
          leadingIcon={<MapPinPlus aria-hidden="true" />}
          onClick={() => setEditing({ kind: "new" })}
        >
          Add address
        </Button>
      </div>

      {addresses.data.length === 0 ? (
        <EmptyPage
          slot="EMPTY_ADDRESSES"
          title="No saved addresses"
          description="Add one now or at checkout."
        />
      ) : (
        <ul className="grid gap-4 lg:grid-cols-2" aria-label="Saved addresses">
          {addresses.data.map((a) => {
            const id = a.addressId;
            return (
              <li key={id}>
                <div className="border-border bg-surface-paper flex h-full gap-3 rounded-[22px] border p-4 sm:p-5">
                  <span
                    aria-hidden="true"
                    className="bg-accent-coral-subtle text-accent-coral-text grid size-10 shrink-0 place-items-center rounded-full"
                  >
                    <MapPin className="size-5" />
                  </span>
                  <div className="flex min-w-0 flex-1 flex-col gap-3">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-text font-bold">{a.recipientName}</span>
                      <Badge>{LABEL_TEXT[a.label] ?? a.label}</Badge>
                      {a.isDefault && <Badge tone="accent">Default</Badge>}
                      {a.isServiceable === false && <Badge tone="warning">Not delivered yet</Badge>}
                    </div>
                    <address className="text-text-muted flex-1 text-sm not-italic">
                      {a.line1}
                      {a.line2 && `, ${a.line2}`}
                      {a.landmark && `, ${a.landmark}`}, {a.locality}, {a.cityName} {a.pincode}
                      <br />
                      {a.phone}
                    </address>
                    <div className="flex flex-wrap gap-2">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => setEditing({ kind: "edit", address: a })}
                      >
                        Edit
                      </Button>
                      {!a.isDefault && (
                        <Button
                          size="sm"
                          variant="ghost"
                          loading={busyId === `${id}-default`}
                          onClick={() =>
                            void run(
                              `${id}-default`,
                              () => makeDefaultAddress(browserHttp(), id),
                              "Default address updated",
                            )
                          }
                        >
                          Make default
                        </Button>
                      )}
                      <Button
                        size="sm"
                        variant="ghost"
                        loading={busyId === `${id}-delete`}
                        aria-label={`Delete address ${a.line1}`}
                        onClick={() =>
                          void run(
                            `${id}-delete`,
                            () => deleteAddress(browserHttp(), id),
                            "Address deleted",
                          )
                        }
                      >
                        Delete
                      </Button>
                    </div>
                  </div>
                </div>
              </li>
            );
          })}
        </ul>
      )}

      <Dialog open={editing !== null} onOpenChange={(open) => !open && setEditing(null)}>
        {editing && (
          <DialogContent title={editing.kind === "new" ? "New address" : "Edit address"} size="lg">
            <AddressForm
              {...(editing.kind === "edit" ? { address: editing.address } : {})}
              onCancel={() => setEditing(null)}
              onSubmit={async (input) => {
                try {
                  if (editing.kind === "new") await addAddress(browserHttp(), input);
                  else
                    await replaceAddress(
                      browserHttp(),
                      editing.address.addressId,
                      input,
                      editing.address.version,
                    );
                } catch (e) {
                  if (hasCode(e, "CONCURRENCY_CONFLICT")) {
                    await refresh();
                    setEditing(null);
                    toast({
                      title: "This address was changed elsewhere. Please review it and try again.",
                      tone: "warning",
                    });
                    return;
                  }
                  throw e;
                }
                await refresh();
                setEditing(null);
                toast({ title: "Address saved", tone: "success" });
              }}
            />
          </DialogContent>
        )}
      </Dialog>
    </div>
  );
}
