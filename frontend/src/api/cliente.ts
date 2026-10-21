import createClient from "openapi-fetch";

import type { paths } from "./schema";

/**
 * Cliente tipado gerado de docs/openapi/v1.json (pnpm gen:api). Fase 2: middleware com access token
 * em memória, refresh single-flight em 401 (cookie HttpOnly + X-Requested-With) e X-Correlation-Id.
 */
export const api = createClient<paths>({ baseUrl: "/" });
