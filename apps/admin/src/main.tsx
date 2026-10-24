import { Toaster, TooltipProvider } from "@fresh/ui";
import { QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { RouterProvider } from "react-router";
import { createAdminRouter } from "./app/router";
import { ServicesProvider, createAppServices } from "./app/services";
import { DemoBanner, isDemoMode } from "./components/DemoBanner";
import "./styles.css";

// Theme: remembered choice, else the system preference (docs/design §4).
try {
  const saved = window.localStorage.getItem("fresh-orbit.admin.theme");
  const dark = saved ? saved === "dark" : window.matchMedia("(prefers-color-scheme: dark)").matches;
  document.documentElement.dataset.theme = dark ? "dark" : "light";
} catch {
  document.documentElement.dataset.theme = "light";
}

const services = createAppServices({ apiOrigin: import.meta.env.VITE_API_ORIGIN });
const router = createAdminRouter();
const root = document.getElementById("root");
if (!root) throw new Error("Missing #root element.");

createRoot(root).render(
  <StrictMode>
    <ServicesProvider services={services}>
      <QueryClientProvider client={services.queryClient}>
        <TooltipProvider>
          <DemoBanner enabled={isDemoMode(import.meta.env.VITE_DEMO_MODE)} />
          <RouterProvider router={router} />
          <Toaster />
        </TooltipProvider>
      </QueryClientProvider>
    </ServicesProvider>
  </StrictMode>,
);
