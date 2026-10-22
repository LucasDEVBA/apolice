/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** "1" liga o mock da API (MSW) no dev server; ignorado no build de produção. */
  readonly VITE_API_MOCK?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
