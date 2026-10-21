import "vitest";
import type { AxeMatchers } from "vitest-axe/matchers";

// vitest-axe 0.1 aumenta a interface antiga (Vi.Assertion); aqui ligamos os matchers à API atual.
declare module "vitest" {
  // eslint-disable-next-line @typescript-eslint/no-empty-object-type, @typescript-eslint/no-explicit-any, @typescript-eslint/no-unused-vars -- a assinatura precisa ser idêntica à do vitest para o merge de interface
  interface Assertion<T = any> extends AxeMatchers {}
  // eslint-disable-next-line @typescript-eslint/no-empty-object-type -- idem
  interface AsymmetricMatchersContaining extends AxeMatchers {}
}
