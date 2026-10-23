import { cookies } from "next/headers";
import { logout } from "@/server/session";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  return logout(request, await cookies());
}
