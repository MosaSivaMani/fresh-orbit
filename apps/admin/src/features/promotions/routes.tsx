import { type RouteObject } from "react-router";
import { screen } from "../../app/route-helpers";

export const promotionsRoutes: RouteObject[] = [
  screen(
    "promotions",
    "promotions.manage",
    async () => (await import("./CouponsListPage")).CouponsListPage,
  ),
  screen(
    "promotions/new",
    "promotions.manage",
    async () => (await import("./CouponEditorPage")).CouponCreatePage,
  ),
  screen(
    "promotions/:couponId",
    "promotions.manage",
    async () => (await import("./CouponEditorPage")).CouponEditorPage,
  ),
];
