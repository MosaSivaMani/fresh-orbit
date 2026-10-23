import next from "@fresh/config/eslint/next";

export default [...next, { ignores: ["next-env.d.ts", "playwright-report/**", "test-results/**"] }];
