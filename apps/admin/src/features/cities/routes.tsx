import { type RouteObject } from "react-router";
import { screen } from "../../app/route-helpers";

export const citiesRoutes: RouteObject[] = [
  screen("cities", "locations.manage", async () => (await import("./CitiesPage")).CitiesPage),
  screen(
    "cities/:cityId",
    "locations.manage",
    async () => (await import("./CityDetailPage")).CityDetailPage,
  ),
  screen(
    "locations/:locationId",
    "locations.manage",
    async () => (await import("./LocationDetailPage")).LocationDetailPage,
  ),
  screen(
    "service-areas/:serviceAreaId",
    "locations.manage",
    async () => (await import("./ServiceAreaPage")).ServiceAreaPage,
  ),
];
