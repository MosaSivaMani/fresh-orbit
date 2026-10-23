import { type RouteObject } from "react-router";
import { screen } from "../../app/route-helpers";

export const customersRoutes: RouteObject[] = [
  screen("customers", "customers.view", () =>
    import("./CustomersListPage").then((m) => m.CustomersListPage),
  ),
  screen("customers/:customerId", "customers.view", () =>
    import("./CustomerDetailPage").then((m) => m.CustomerDetailPage),
  ),
];
