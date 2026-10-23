// RFC 7807 responses produced by the website's own route handlers (same shape as FreshOrbit.Api, docs/api §1.6).

export function problemResponse(
  status: number,
  code: string,
  title: string,
  extra: Record<string, unknown> = {},
): Response {
  return new Response(JSON.stringify({ status, code, title, errors: {}, ...extra }), {
    status,
    headers: {
      "Content-Type": "application/problem+json; charset=utf-8",
      "Cache-Control": "no-store",
    },
  });
}
