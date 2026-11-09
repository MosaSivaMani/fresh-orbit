import { Clock, Mail, Phone, User } from "lucide-react";
import { type GrievanceOfficer, type PublicBusinessProfile } from "@/api/types/locations";

/**
 * The grievance officer the business profile names (D374), or null: shown only with a name and an e-mail, never
 * guessed and never taken from web configuration.
 */
export function grievanceOfficerOf(profile: PublicBusinessProfile | null): GrievanceOfficer | null {
  const officer = profile?.grievanceOfficer;
  if (!officer || !officer.name.trim() || !officer.email.trim()) return null;
  return officer;
}

/**
 * The grievance officer block on the help page (L-7, D300, D374): name, designation, e-mail, phone and response
 * time, exactly as the business profile holds them. Each optional line appears only when the server sends it.
 */
export function GrievanceOfficerCard({ officer }: { officer: GrievanceOfficer }) {
  const phone = officer.phone?.replace(/\s+/g, "");
  return (
    <section
      id="grievance"
      aria-labelledby="grievance-heading"
      className="bg-surface border-border flex flex-col gap-3 rounded-[20px] border p-5 text-sm"
    >
      <h2 id="grievance-heading" className="font-display text-text text-lg font-extrabold">
        Grievance officer
      </h2>
      <ul className="text-text flex flex-col gap-2">
        <li className="flex items-center gap-2">
          <User aria-hidden="true" className="text-accent-text size-4 shrink-0" />
          <span>
            <span className="font-semibold">{officer.name}</span>
            {officer.designation && <>, {officer.designation}</>}
          </span>
        </li>
        <li className="flex items-center gap-2">
          <Mail aria-hidden="true" className="text-accent-text size-4 shrink-0" />
          <a
            href={`mailto:${officer.email}`}
            className="font-semibold break-all underline-offset-4 hover:underline"
          >
            {officer.email}
          </a>
        </li>
        {phone && (
          <li className="flex items-center gap-2">
            <Phone aria-hidden="true" className="text-accent-text size-4 shrink-0" />
            <a href={`tel:${phone}`} className="font-semibold underline-offset-4 hover:underline">
              {officer.phone}
            </a>
          </li>
        )}
        {officer.responseTime && (
          <li className="flex items-center gap-2">
            <Clock aria-hidden="true" className="text-accent-text size-4 shrink-0" />
            {officer.responseTime}
          </li>
        )}
      </ul>
    </section>
  );
}
