import { cookies } from "next/headers";
import { verifyOtp } from "@/server/session";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  return verifyOtp(request, await cookies());
}
