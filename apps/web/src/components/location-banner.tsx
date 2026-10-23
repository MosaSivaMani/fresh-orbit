import { MapPin } from "lucide-react";
import { ChangePincodeButton } from "./change-pincode-button";

/** Shown on catalog pages: no pincode yet, or the stored one is no longer served. */
export function LocationBanner({
  pincode,
  pincodeRefused,
}: {
  pincode: string | null;
  pincodeRefused: boolean;
}) {
  if (pincode && !pincodeRefused) return null;
  return (
    <div
      role="status"
      className="bg-highlight-subtle text-highlight-text flex flex-col gap-3 rounded-xl p-4 sm:flex-row sm:items-center sm:justify-between"
    >
      <p className="flex items-start gap-2 text-sm">
        <MapPin aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
        {pincodeRefused
          ? `We no longer deliver to ${pincode}. Prices are hidden until you choose a pincode we serve.`
          : "Set your delivery pincode to see prices, stock and delivery slots for your area."}
      </p>
      <ChangePincodeButton label={pincodeRefused ? "Change pincode" : "Set pincode"} />
    </div>
  );
}
