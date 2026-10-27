import { HttpResponse, http } from "msw";
import { emptyCart } from "../fixtures";

/** Browser calls go to this site's proxy (`http://localhost:3000/api/v1/...`) and BFF (`/bff/auth/...`). */
export const API = "http://localhost:3000/api/v1";
export const BFF = "http://localhost:3000/bff/auth";

export const defaultHandlers = [
  http.get(`${API}/cart`, () => HttpResponse.json(emptyCart)),
  // No quote unless a test gives one: the product panel then says the amounts appear in the cart.
  http.get(`${API}/price-quote`, () =>
    HttpResponse.json({ status: 404, code: "NOT_FOUND" }, { status: 404 }),
  ),
  http.post(`${BFF}/refresh`, () =>
    HttpResponse.json({ status: 401, code: "REFRESH_TOKEN_INVALID" }, { status: 401 }),
  ),
];
