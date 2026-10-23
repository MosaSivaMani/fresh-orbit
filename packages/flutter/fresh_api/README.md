# fresh_api (Dart, generated)

Typed Dart client for FreshOrbit.Api, generated from the same OpenAPI document as `@fresh/api-client`
(`packages/api-client/openapi.json`). Used by the Flutter app in `apps/mobile` (D28).

**Status:** scaffold only. Generation runs after Phase 3 merges, when the API exposes its endpoints
(today `openapi.json` has no paths). Nothing in this folder is hand-written except this README.

## Generate

Uses [OpenAPI Generator](https://openapi-generator.tech) (Apache-2.0), `dart-dio` generator, via Docker so no
Java install is needed. Run from the repository root:

```bash
# 1. Refresh the spec from a running API (Development environment), or use the committed one
pnpm --filter @fresh/api-client fetch-spec            # default http://localhost:5080/openapi/v1.json

# 2. Generate the Dart package into packages/flutter/fresh_api
docker run --rm -v "$PWD:/local" openapitools/openapi-generator-cli:"$OAG_VERSION" generate \
  -i /local/packages/api-client/openapi.json \
  -g dart-dio \
  -o /local/packages/flutter/fresh_api \
  --additional-properties=pubName=fresh_api,pubDescription="Fresh Orbit API client (generated)",serializationLibrary=json_serializable,dateLibrary=core,nullableFields=true

# 3. Run code generation inside the package (json_serializable)
cd packages/flutter/fresh_api && dart run build_runner build --delete-conflicting-outputs
```

Set `OAG_VERSION` to a released generator tag (for example `v7.x.y`, at least two weeks old) and record it here when generation first runs; bump it deliberately. Without Docker, use the jar
(`java -jar openapi-generator-cli-$OAG_VERSION.jar generate ...` with the same arguments).

## Rules

- Do not edit generated files; change the backend contract and regenerate.
- Commit the generated output so the mobile app builds without Docker.
- Keep `README.md` and `.openapi-generator-ignore` (add `README.md` to it so generation does not overwrite this file).
- Money stays `int` paise, weight `int` grams, rates `int` basis points; format for display only
  (see `docs/design/README.md` §7). Business values always come from the server.
- Auth: add a Dio interceptor that sends `Authorization: Bearer`, and on 401 `TOKEN_EXPIRED` calls
  `POST /api/v1/auth/refresh` once (single flight) and retries, mirroring `@fresh/core` `createAuthFetch`.
- Problem Details: map `DioException.response.data` to an `ApiError { status, code, errors, traceId }` and branch on
  `code`, never on message text.
