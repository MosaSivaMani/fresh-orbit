import { type RouteObject } from "react-router";
import { screen } from "../../app/route-helpers";

// Static segments before ":productId" so "new" and "categories" are not read as ids.
export const productsRoutes: RouteObject[] = [
  screen(
    "products",
    "catalog.manage",
    async () => (await import("./ProductsListPage")).ProductsListPage,
  ),
  screen(
    "products/new",
    "catalog.manage",
    async () => (await import("./ProductCreatePage")).ProductCreatePage,
  ),
  screen(
    "products/categories",
    "catalog.manage",
    async () => (await import("./CategoriesPage")).CategoriesPage,
  ),
  screen(
    "products/:productId",
    "catalog.manage",
    async () => (await import("./ProductEditorPage")).ProductEditorPage,
  ),
];
