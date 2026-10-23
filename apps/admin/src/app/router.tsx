import { type RouteObject, createBrowserRouter } from "react-router";
import { NotFound } from "../components/page";
import { LoginPage } from "../features/auth/LoginPage";
import { citiesRoutes } from "../features/cities/routes";
import { customersRoutes } from "../features/customers/routes";
import { dashboardRoutes } from "../features/dashboard/routes";
import { deliveryRoutes } from "../features/delivery/routes";
import { inventoryRoutes } from "../features/inventory/routes";
import { ordersRoutes } from "../features/orders/routes";
import { paymentsRoutes } from "../features/payments/routes";
import { pricingRoutes } from "../features/pricing/routes";
import { productsRoutes } from "../features/products/routes";
import { promotionsRoutes } from "../features/promotions/routes";
import { reportsRoutes } from "../features/reports/routes";
import { settingsRoutes } from "../features/settings/routes";
import { AdminLayout } from "./AdminLayout";

/** Every admin route. Feature folders own their route lists; permissions per route are in each list. */
export const routes: RouteObject[] = [
  { path: "/login", element: <LoginPage /> },
  {
    path: "/",
    element: <AdminLayout />,
    children: [
      ...dashboardRoutes,
      ...ordersRoutes,
      ...deliveryRoutes,
      ...inventoryRoutes,
      ...productsRoutes,
      ...pricingRoutes,
      ...promotionsRoutes,
      ...citiesRoutes,
      ...customersRoutes,
      ...reportsRoutes,
      ...paymentsRoutes,
      ...settingsRoutes,
      { path: "*", element: <NotFound /> },
    ],
  },
];

export function createAdminRouter() {
  return createBrowserRouter(routes);
}
