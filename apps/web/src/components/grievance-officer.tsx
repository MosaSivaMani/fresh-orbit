import { Clock, Mail, User } from "lucide-react";
import { type GrievanceOfficer } from "@/server/config";

/**
 * The grievance officer block on the help page (L-7, D300): name, designation, email and response time, exactly as
 * configured (`FRESH_GRIEVANCE_*`). The page shows it only when `getGrievanceOfficer()` returns an officer.
 */
export function GrievanceOfficerCard({ officer }: { officer: GrievanceOfficer }) {
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
        <li className="flex items-center gap-2">
          <Clock aria-hidden="true" className="text-accent-text size-4 shrink-0" />
          {officer.responseTime}
        </li>
      </ul>
    </section>
  );
}
