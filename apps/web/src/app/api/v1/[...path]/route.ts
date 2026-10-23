import { cookies } from "next/headers";
import { CART_COOKIE } from "@/lib/cookies";
import { guestCartCookieOptions, proxyToApi } from "@/server/api-proxy";

export const dynamic = "force-dynamic";

async function handle(request: Request, context: RouteContext<"/api/v1/[...path]">) {
  const { path } = await context.params;
  const jar = await cookies();
  const { response, issuedCartToken } = await proxyToApi(request, path, jar);
  if (issuedCartToken) jar.set(CART_COOKIE, issuedCartToken, guestCartCookieOptions);
  return response;
}

export { handle as DELETE, handle as GET, handle as PATCH, handle as POST, handle as PUT };
