import { Badge, Button, StatusBadge } from "@fresh/ui";
import { type ReactNode } from "react";
import { Link } from "react-router";
import { useStoreReadiness } from "../../api/operations";
import { type ReadinessStatus, type StoreReadiness } from "../../api/types/operations";
import { useCan } from "../../app/services";
import { PageHeader, QueryView, Section } from "../../components/page";
import { LOCATION_STATUS } from "../../lib/status";
import { READINESS_STATUS } from "./shared";

// D358 (audit BF-3): one page listing what still blocks real trading. Every value is computed by the server
// (`GET /admin/settings/store-readiness`); this page only renders it and links to where each item is fixed.

const PROFILE_FIELDS: Record<string, string> = {
  legalName: "Legal name",
  address: "Registered address",
  gstin: "GSTIN",
  timeZone: "Time zone",
};

const CHANNELS: Record<string, { label: string; settings: string }> = {
  SMS: { label: "SMS (DLT)", settings: "SMS_DLT_* settings with the DLT provider" },
  EMAIL: { label: "E-mail", settings: "EMAIL_PROVIDER=SMTP and SMTP_* settings" },
  PUSH: { label: "Push notifications", settings: "PUSH_PROVIDER=FCM and FCM_* settings" },
};

const PROVIDERS: Record<string, string> = {
  DLT: "DLT provider",
  SMTP: "SMTP server",
  FCM: "Firebase Cloud Messaging",
  DISABLED: "Switched off",
  DEVELOPMENT: "Development sender, delivers nothing",
};

export function StoreReadinessPage() {
  const readiness = useStoreReadiness();
  return (
    <>
      <PageHeader
        title="Store readiness"
        description="What must be in place before the store takes real orders. Each item links to where it is fixed."
        actions={
          <Button
            variant="outline"
            size="sm"
            loading={readiness.isFetching}
            onClick={() => void readiness.refetch()}
          >
            Check again
          </Button>
        }
      />
      <QueryView query={readiness} what="readiness report">
        {(report) => <Report report={report} />}
      </QueryView>
    </>
  );
}

function Report({ report }: { report: StoreReadiness }) {
  const can = useCan();
  const canFixLocations = can("locations.manage");

  return (
    <div className="flex flex-col gap-4">
      <div role="status" className="flex items-center gap-2">
        {report.isReady ? (
          <Badge tone="success">Ready to trade</Badge>
        ) : (
          <Badge tone="danger">Not ready</Badge>
        )}
        {!report.isReady && (
          <span className="text-text-muted text-sm">Fix every item marked Missing below.</span>
        )}
      </div>

      <Section
        title="Business profile"
        description="Printed on every invoice. Checkout refuses orders until the legal name, GSTIN and time zone are saved."
      >
        <ul className="divide-border flex flex-col divide-y">
          {report.businessProfile.map((f) => (
            <Row
              key={f.field}
              label={PROFILE_FIELDS[f.field] ?? f.field}
              status={f.status}
              fix={<FixLink to="/settings/business-profile">Open business profile</FixLink>}
            />
          ))}
        </ul>
      </Section>

      <Section
        title="Locations"
        description="Each premises needs its own FSSAI licence number; it is printed on the invoices it delivers."
      >
        <ul className="divide-border flex flex-col divide-y">
          {report.locations.length === 0 ? (
            <Row
              label="No location yet"
              status="MISSING"
              fix={
                canFixLocations ? <FixLink to="/cities">Open cities and locations</FixLink> : null
              }
            />
          ) : (
            report.locations.map((l) => (
              <Row
                key={l.locationId}
                label={`${l.name} (${l.code}) FSSAI licence`}
                detail={<StatusBadge code={l.locationStatus} map={LOCATION_STATUS} />}
                status={l.fssaiLicenseStatus}
                fix={
                  canFixLocations ? (
                    <FixLink to={`/locations/${l.locationId}`}>Open {l.name}</FixLink>
                  ) : null
                }
              />
            ))
          )}
        </ul>
      </Section>

      <Section
        title="Messages"
        description="Server settings, changed on the server and not in this dashboard (runbook: security configuration). Only whether each is set is shown, never its value."
      >
        <ul className="divide-border flex flex-col divide-y">
          {report.channels.map((c) => (
            <Row
              key={c.channel}
              label={CHANNELS[c.channel]?.label ?? c.channel}
              detail={
                <span className="text-text-muted text-sm">
                  {PROVIDERS[c.provider] ?? c.provider}
                </span>
              }
              status={c.status}
              fix={
                <span className="text-text-muted text-sm">
                  Set {CHANNELS[c.channel]?.settings ?? "the provider settings"}
                </span>
              }
            />
          ))}
        </ul>
      </Section>
    </div>
  );
}

function Row({
  label,
  status,
  detail,
  fix,
}: {
  label: string;
  status: ReadinessStatus;
  detail?: ReactNode;
  fix: ReactNode;
}) {
  return (
    <li
      aria-label={label}
      className="flex flex-col gap-1 py-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4"
    >
      <span className="flex flex-wrap items-center gap-2">
        <span className="font-medium">{label}</span>
        {detail}
      </span>
      <span className="flex flex-wrap items-center gap-3">
        <StatusBadge code={status} map={READINESS_STATUS} />
        {status === "MISSING" && fix}
      </span>
    </li>
  );
}

function FixLink({ to, children }: { to: string; children: ReactNode }) {
  return (
    <Link to={to} className="text-accent-text text-sm font-semibold hover:underline">
      {children}
    </Link>
  );
}
