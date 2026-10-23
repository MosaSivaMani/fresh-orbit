import path from "node:path";
import type { NextConfig } from "next";

// The Docker image (infra/docker/web.Dockerfile) sets NEXT_OUTPUT=standalone to get a self-contained server with
// only the traced node_modules. Tracing starts at the monorepo root so workspace packages are included. Local
// `next dev` / `next start` are unchanged.
const standalone = process.env.NEXT_OUTPUT === "standalone";

const securityHeaders = [
  { key: "X-Content-Type-Options", value: "nosniff" },
  { key: "X-Frame-Options", value: "DENY" },
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=()" },
];

const nextConfig: NextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  ...(standalone
    ? { output: "standalone" as const, outputFileTracingRoot: path.join(process.cwd(), "..", "..") }
    : {}),
  images: {
    // The API already serves WebP renditions (320 / 640 / 1280 px, CAT-R16); re-optimising them here would only
    // add CPU work and a second cache. next/image still gives lazy loading and reserved space.
    unoptimized: true,
  },
  async headers() {
    return [{ source: "/:path*", headers: securityHeaders }];
  },
};

export default nextConfig;
