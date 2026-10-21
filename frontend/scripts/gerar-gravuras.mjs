// Gera as gravuras (guilloché) e o selo da marca como SVG estático, em build.
// Por quê: a roseta XL tem ~40 mil pontos; calcular isso no runtime custaria CPU e CLS
// (DESIGN.md §4.1). Rode `pnpm gen:gravuras` e versione os arquivos gerados.
import { mkdir, writeFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { optimize } from "svgo";

const RAIZ = join(dirname(fileURLToPath(import.meta.url)), "..");
const DESTINO = join(RAIZ, "src/ui/marca/gravuras");
const TAU = Math.PI * 2;
const OURO_LINHA = "#C9A86A"; // --gold-line: igual nos dois temas.

/** Curva paramétrica da referência: raio modulado por duas senoides, com cópias giradas. */
// ~10 amostras por onda da senoide mais rápida bastam para a curva parecer contínua.
function anel(cx, cy, raio, a, n, b, m, copias, passos = Math.max(360, Math.max(n, m) * 10)) {
  const caminhos = [];
  for (let j = 0; j < copias; j++) {
    const giro = (j / copias) * (TAU / n);
    let d = "";
    for (let s = 0; s <= passos; s++) {
      const t = (s / passos) * TAU;
      const r = raio + a * Math.sin(n * t) + b * Math.sin(m * t + j * 0.9);
      const x = cx + r * Math.cos(t + giro);
      const y = cy + r * Math.sin(t + giro);
      d += `${s ? "L" : "M"}${x.toFixed(2)} ${y.toFixed(2)}`;
    }
    caminhos.push(`${d}Z`);
  }
  return caminhos;
}

// A opacidade entra DEPOIS do SVGO: com floatPrecision 1 ele arredondaria 0,22 → 0,2 e
// 0,26 → 0,3, tirando a gravura da faixa do DESIGN.md §4.1.
const MARCA_OPACIDADE = "data-opacidade";

function roseta(tamanho, camadas, espessura) {
  const caminhos = camadas
    .flat()
    .map((d) => `<path d="${d}"/>`)
    .join("");
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${tamanho} ${tamanho}"><g fill="none" stroke="${OURO_LINHA}" stroke-width="${espessura}" ${MARCA_OPACIDADE}="1">${caminhos}</g></svg>`;
}

// Folha dourada do DESIGN.md §4.1 (vale sobre a referência): #8E6E3C → #D9BF8C → #A9884F.
function selo() {
  const petalas = anel(16, 16, 9.2, 2.6, 8, 0, 1, 3, 400)
    .map((d) => `<path d="${d}" stroke-width="0.85"/>`)
    .join("");
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32"><defs><linearGradient id="folha" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#8E6E3C"/><stop offset=".5" stop-color="#D9BF8C"/><stop offset="1" stop-color="#A9884F"/></linearGradient></defs><g fill="none" stroke="url(#folha)"><circle cx="16" cy="16" r="15" stroke-width="1.4"/>${petalas}<circle cx="16" cy="16" r="3" stroke-width="1.2"/></g></svg>`;
}

const c = { xl: 380, lg: 260, sm: 130 };
// [svg, opacidade]: xl/lg/sm entre 0,22 e 0,30; o anel do estado vazio a 0,18 (DESIGN.md §4.1).
const ARQUIVOS = {
  "roseta-xl.svg": [
    roseta(
      760,
      [
        anel(c.xl, c.xl, 96, 22, 12, 6, 24, 10),
        anel(c.xl, c.xl, 178, 26, 24, 8, 48, 12),
        anel(c.xl, c.xl, 262, 18, 36, 7, 72, 12),
        anel(c.xl, c.xl, 330, 12, 48, 5, 96, 8),
      ],
      0.7,
    ),
    0.3,
  ],
  "roseta-lg.svg": [
    roseta(
      520,
      [
        anel(c.lg, c.lg, 70, 16, 12, 4, 24, 9),
        anel(c.lg, c.lg, 128, 20, 24, 6, 48, 11),
        anel(c.lg, c.lg, 196, 14, 36, 5, 72, 10),
      ],
      0.6,
    ),
    0.24,
  ],
  "roseta-sm.svg": [
    roseta(
      260,
      [anel(c.sm, c.sm, 44, 10, 10, 3, 20, 8), anel(c.sm, c.sm, 84, 12, 20, 4, 40, 10)],
      0.6,
    ),
    0.26,
  ],
  "anel-vazio.svg": [roseta(96, [anel(48, 48, 40, 2.2, 24, 1, 48, 3, 600)], 0.6), 0.18],
  "selo.svg": [selo(), null],
};

await mkdir(DESTINO, { recursive: true });
for (const [nome, [svg, opacidade]] of Object.entries(ARQUIVOS)) {
  const { data } = optimize(svg, {
    multipass: true,
    floatPrecision: 1,
    plugins: ["preset-default"],
  });
  const final =
    opacidade === null ? data : data.replace(`${MARCA_OPACIDADE}="1"`, `opacity="${opacidade}"`);
  if (opacidade !== null && !final.includes(`opacity="${opacidade}"`)) {
    throw new Error(`${nome}: o SVGO removeu a marca de opacidade; ajuste o script.`);
  }
  await writeFile(join(DESTINO, nome), `${final}\n`);
  console.log(
    `${nome}: ${(Buffer.byteLength(final) / 1024).toFixed(1)} KB · opacidade ${String(opacidade ?? "—")}`,
  );
}
// O favicon é o próprio selo.
await writeFile(
  join(RAIZ, "public/favicon.svg"),
  `${optimize(selo(), { floatPrecision: 2 }).data}\n`,
);
