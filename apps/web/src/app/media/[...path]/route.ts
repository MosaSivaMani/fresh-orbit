// Public images (`/media/*`) from FreshOrbit.Api on this site's origin. The API returns image URLs as paths
// ("/media/products/…/640.webp"); without this the website would ask itself for them and show broken pictures.
// Read-only, images only, no cookies or headers forwarded.
import { ConfigurationMissingError, getApiOrigin } from "@/server/config";
import { toApiPath } from "@/server/api-proxy";

const IMAGE = /\.(webp|jpe?g|png)$/i;

export async function GET(_request: Request, context: RouteContext<"/media/[...path]">) {
  const { path } = await context.params;
  const joined = toApiPath(path);
  if (!joined || !IMAGE.test(joined)) return new Response(null, { status: 404 });
  let origin: string;
  try {
    origin = getApiOrigin();
  } catch (error) {
    if (error instanceof ConfigurationMissingError) return new Response(null, { status: 503 });
    throw error;
  }
  const upstream = await fetch(`${origin}/media/${joined}`, { cache: "no-store" }).catch(
    () => null,
  );
  if (!upstream || !upstream.ok || !upstream.body) {
    return new Response(null, { status: upstream?.status === 404 ? 404 : 502 });
  }
  const headers = new Headers({
    "content-type": upstream.headers.get("content-type") ?? "application/octet-stream",
    "cache-control": upstream.headers.get("cache-control") ?? "public, max-age=3600",
  });
  const etag = upstream.headers.get("etag");
  if (etag) headers.set("etag", etag);
  return new Response(upstream.body, { status: 200, headers });
}
