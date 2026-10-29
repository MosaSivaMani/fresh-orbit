import { receiveBrowserError } from "@/server/monitoring";
import { checkSameOrigin } from "@/server/session";

export const dynamic = "force-dynamic";

/** An error boundary in the page reports what it caught (W-7, D316). */
export async function POST(request: Request) {
  return receiveBrowserError(request, checkSameOrigin);
}
