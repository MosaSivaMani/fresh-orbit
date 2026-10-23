import { cookies } from "next/headers";
import { refreshSession } from "@/server/session";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  return refreshSession(request, await cookies());
}
