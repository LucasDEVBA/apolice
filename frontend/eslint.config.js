import js from "@eslint/js";
import { defineConfig, globalIgnores } from "eslint/config";
import jsxA11y from "eslint-plugin-jsx-a11y";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import globals from "globals";
import tseslint from "typescript-eslint";

export default defineConfig([
  globalIgnores([
    "dist",
    "coverage",
    "playwright-report",
    "test-results",
    ".screens",
    "src/api/schema.d.ts",
  ]),
  {
    files: ["**/*.{ts,tsx}"],
    extends: [
      js.configs.recommended,
      tseslint.configs.strictTypeChecked,
      tseslint.configs.stylisticTypeChecked,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
      jsxA11y.flatConfigs.strict,
    ],
    languageOptions: {
      ecmaVersion: 2023,
      globals: globals.browser,
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      "@typescript-eslint/restrict-template-expressions": ["error", { allowNumber: true }],
      "@typescript-eslint/consistent-type-imports": ["error", { fixStyle: "inline-type-imports" }],
      "@typescript-eslint/no-confusing-void-expression": ["error", { ignoreArrowShorthand: true }],
      "@typescript-eslint/no-misused-promises": [
        "error",
        { checksVoidReturn: { attributes: false } },
      ],
      // Cor fixa em componente (hex, rgb/hsl/oklch, inclusive em classe arbitrária e template):
      // a cor vem sempre de token semântico (DESIGN.md §2.7).
      "no-restricted-syntax": [
        "error",
        {
          selector: "Literal[value=/#[0-9a-fA-F]{3,8}\\b|(^|[^a-z])(rgb|hsl|oklch)a?\\(/i]",
          message: "Cor fixa em componente: use um token semântico (DESIGN.md §3).",
        },
        {
          selector:
            "TemplateElement[value.raw=/#[0-9a-fA-F]{3,8}\\b|(^|[^a-z])(rgb|hsl|oklch)a?\\(/i]",
          message: "Cor fixa em componente: use um token semântico (DESIGN.md §3).",
        },
      ],
    },
  },
  {
    // Testes e configs: liberdade para utilitários de teste, sem regra de HMR.
    files: ["**/*.test.{ts,tsx}", "src/test/**", "e2e/**", "*.config.ts"],
    rules: {
      "react-refresh/only-export-components": "off",
      "@typescript-eslint/no-non-null-assertion": "off",
    },
  },
  {
    files: ["e2e/**", "*.config.ts"],
    languageOptions: { globals: globals.node },
  },
  {
    files: ["**/*.{js,mjs}"],
    extends: [js.configs.recommended],
    languageOptions: { globals: { ...globals.node, ...globals.browser } },
  },
]);
