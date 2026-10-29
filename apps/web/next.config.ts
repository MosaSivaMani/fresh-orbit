import path from "node:path";
import type { NextConfig } from "next";

// The Docker image (infra/docker/web.Dockerfile) sets NEXT_OUTPUT=standalone to get a self-contained server with
// only the traced node_modules. Tracing starts at the monorepo root so workspace packages are included. Local
// `next dev` / `next start` are unchanged.
const standalone = process.env.NEXT_OUTPUT === "standalone";

// `next dev` in a GitHub Codespace is opened through a forwarded https URL (`<name>-3000.<domain>`); let that origin
// load the dev-only resources. `allowedDevOrigins` has no effect on `next build` / `next start`.
const codespacesDomain = process.env.GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN?.trim();

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
  ...(codespacesDomain ? { allowedDevOrigins: [`*.${codespacesDomain}`] } : {}),
  images: {
    // The API already serves WebP renditions (320 / 640 / 1280 px, CAT-R16); re-optimising them here would only
    // add CPU work and a second cache. next/image still gives lazy loading and reserved space. With this set,
    // next/image drops `srcset`, so the product gallery draws the server's renditions itself, and the home hero
    // uses WebP copies saved at their drawn size (W-8, W-9, D318).
    unoptimized: true,
  },
  async headers() {
    return [{ source: "/:path*", headers: securityHeaders }];
  },
};

export default nextConfig;
