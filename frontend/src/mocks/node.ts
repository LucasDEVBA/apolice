/** Servidor MSW dos testes (Vitest + jsdom). */
import { setupServer } from "msw/node";

import { handlers } from "./handlers";

export const servidorMock = setupServer(...handlers);
