# Apólice: guia de design e implementação

> Fonte da verdade visual do frontend (`frontend/`: Vite + React 19 + Tailwind CSS 4 + Radix + Recharts,
> conforme `docs/SPEC.md` §11). Quando a referência em `referencia/` e este guia divergirem,
> **vale este guia**. A seção 10 lista as correções feitas sobre a referência.
>
> Referência visual (conceito vencedor, renderizado em 1440 px e 390 px):
> [login](referencia/login.png) · [dashboard](referencia/dashboard.png) · [apólice](referencia/apolice.png) ·
> [cotação](referencia/cotacao.png) · [dashboard mobile](referencia/dashboard-mobile.png) · [HTML](referencia/index.html)

---

## 1. Decisão

**Vencedor: Conceito A, "Institucional premium"** (azul-marinho + fio dourado, títulos e números em serifa,
apólice tratada como certificado gravado).

| Conceito | Juiz 2 (viu as telas) | Soma das 6 notas | Leitura |
|---|---|---|---|
| **A · Institucional premium** | **1º** | **51,5** | Identidade de seguradora mais forte, hierarquia clara, melhor mobile |
| C · Operacional de alta densidade | 2º | 49,5 | O mais "ERP real" no uso diário, mas plano e sem âncora visual |
| B · Insurtech moderna | 3º | 43,0 | Ótimas microinterações; visual de fintech genérica, zero cortado no dinheiro, barras sem contraste |

O juiz 1 não emitiu veredito (os PNGs tinham sumido no reboot) e deixou só uma hipótese textual, com base
em C. O juiz 2 avaliou as imagens regeradas e escolheu A. A revisão visual da direção de design confirma A:
- é o único dos três que um recrutador reconhece como **produto de seguradora**, e não como template de SaaS;
- atende a preferência do dono por algo rico e premium sem perder a sobriedade corporativa;
- dar densidade operacional ao A (enxertos do C) é barato; dar identidade ao C seria redesenhá-lo.

O preço dessa escolha é densidade menor. A seção 9 importa do C a operação (tabelas, filas, abas, modo
compacto) e do B as microinterações da cotação.

---

## 2. Direção e princípios

**Direção:** *a apólice é um documento oficial.* O produto passa a confiança de um certificado
(marinho profundo, gravura guilloché, serifa nos números que importam) e opera com a precisão de um ERP
(tabelas tabulares, memória de cálculo auditável, filas de trabalho).

1. **Documento, não dashboard genérico.** Superfícies "oficiais" (face da apólice, bloco do prêmio total,
   painel do login) são marinho com gravura. Todo o resto é claro, limpo e de borda fina.
2. **Dourado é fio, não tinta.** O dourado aparece só em traços, ícones ativos, marcadores e texto sobre
   marinho. Nunca preenche botão, card ou área grande.
3. **Número é protagonista, com hierarquia.** No máximo 2 números-herói por tela (serifa, `R$` sobrescrito,
   centavos reduzidos). KPIs em serifa com valor inteiro legível. Tabelas em sans tabular e valor completo.
4. **Toda conta fecha.** Memória de cálculo visível, cada fator explicado em linguagem de negócio,
   somas conferíveis. Um tech lead de seguros vai somar as colunas.
5. **Densidade sob controle de quem usa.** O padrão é confortável; o analista pode ligar o modo compacto.
6. **Estado nunca só por cor.** Todo status leva ponto ou ícone + rótulo, e todo delta leva seta + sinal.
7. **Token antes de cor.** Nenhum hex dentro de componente e nenhum `dark:` em componente: a cor vem
   sempre de token semântico, que já resolve claro e escuro.
8. **Dados honestos.** A UI não exibe campo que o domínio não tem (ver §10.2). Dado de demonstração é rotulado.

**Não fazer:** gradientes (exceções: o traço de "folha dourada" da marca e a faixa de 6 px no topo da face da apólice),
glassmorphism, sombras em cards, emojis, rótulos em CAIXA-ALTA espaçada como padrão, preto puro,
contagem animada de números, ilustrações genéricas de "pessoas felizes", verde de banco digital.

---

## 3. Tokens

### 3.1 Arquitetura

- **Primitivos** (paleta crua) → **semânticos** (papel na UI). Componentes só consomem semânticos.
- Tema por `light-dark()` + `color-scheme` (Baseline desde mai/2024): um token, dois valores.
  `:root { color-scheme: light dark }` segue o sistema; `data-theme="light|dark"` no `<html>` força.
  A preferência (Sistema/Claro/Escuro) fica em `localStorage` (`apolice.tema`), lida em try/catch.
- As superfícies de marca (sidebar no claro, face da apólice, bloco do total, tooltip, toast) são marinho
  **nos dois temas**; os tokens `--on-navy-*` são fixos.

### 3.2 Primitivos

| Família | Tons (hex) |
|---|---|
| Marinho | 950 `#071427` · 900 `#0B1F3A` · 850 `#102849` · 800 `#16305A` · 700 `#1D3B6B` · 600 `#24508F` |
| Dourado | 200 `#E6D3A8` · 300 `#D9BF8C` · 400 `#C9A86A` · 500 `#B08D57` · 700 `#7A5C2C` |
| Aço / latão (gráficos) | aço 400 `#5E93CF` · latão 500 `#B5862F` |
| Tinta (claro) | `#0E1A2C` · `#44526A` · `#5D697D` |
| Tinta sobre marinho | `#F4F6FA` · `#B8C3D6` · `#8F9DB5` |
| Noite (escuro) | 1000 `#060E1C` · 950 `#0A1528` · 900 `#0F1D33` · 850 `#14243E` · 800 `#15263F` · linha `#233552` |

### 3.3 Semânticos: superfícies, texto, ação

Contraste medido (WCAG 2.1) no par indicado; texto normal exige 4,5:1 e componentes de UI exigem 3:1.

| Token | Claro | Escuro | Uso |
|---|---|---|---|
| `--canvas` | `#F2F4F8` | `#0A1528` | Fundo da área de trabalho (frio, sem creme) |
| `--surface` | `#FFFFFF` | `#0F1D33` | Cards, tabelas, topbar, modais no claro |
| `--surface-2` | `#F7F8FB` | `#14243E` | Cabeçalho de tabela, rodapé de wizard/modal, campo desabilitado |
| `--surface-raised` | `#FFFFFF` | `#15263F` | Popover, menu, modal, toast no escuro |
| `--surface-hover` | `#FAFBFD` | `#13223B` | Hover de linha de tabela e de lista |
| `--surface-selected` | `#EEF2F9` | `#16263F` | Linha selecionada, item de menu ativo |
| `--sidebar` | `#0B1F3A` | `#060E1C` | Sidebar, top bar mobile |
| `--sidebar-active` | `#16305A` | `#13243F` | Item de navegação ativo |
| `--brand-surface` | `#0B1F3A` | `#0B1F3A` | Face da apólice, bloco do prêmio total, tooltip (+ fio dourado no escuro) |
| `--ink` | `#0E1A2C` (17,5:1) | `#E8EDF5` (14,4:1) | Texto principal |
| `--ink-2` | `#44526A` (7,9:1) | `#B4C0D2` (9,2:1) | Texto secundário, rótulos |
| `--ink-3` | `#5D697D` (5,0:1 no canvas) | `#8E9CB3` (5,5:1 no surface-2) | Metadados, placeholders, eixos |
| `--on-navy` / `-2` / `-3` | `#F4F6FA` (15,3) · `#B8C3D6` (9,3) · `#8F9DB5` (6,0) | idem | Texto sobre marinho |
| `--gold-text` | `#7A5C2C` (6,2:1) | `#D9BF8C` (9,5:1) | Texto dourado sobre superfície |
| `--gold-on-navy` | `#D9BF8C` (9,3:1) | idem | Texto/rótulo dourado sobre marinho |
| `--gold-line` | `#C9A86A` | `#C9A86A` | Ícone ativo, gravura, marcador (não texto pequeno) |
| `--link` | `#24508F` (8,0:1) | `#8DB4E8` (7,9:1) | Links e IDs clicáveis |
| `--focus` | `#24508F` | `#8DB4E8` | Anel de foco (em marinho: `--gold-line`, 7,3:1) |
| `--primary-bg` / `-hover` / `-active` | `#0B1F3A` / `#16305A` / `#071427` | `#DCE4F0` / `#FFFFFF` / `#C9D4E5` | Botão primário |
| `--primary-fg` | `#FFFFFF` (16,5:1) | `#0B1F3A` (12,9:1) | Texto do primário |
| `--danger-bg` / `-hover` | `#A3302A` / `#8C2823` | `#B4382F` / `#C4443A` | Botão destrutivo (texto branco: 7,0 · 8,6 · 5,9 · 5,0) |
| `--line` | `#DFE4EC` | `#233552` | Borda de card, cabeçalho de tabela |
| `--line-soft` | `#ECEFF4` | `#1A2A44` | Divisórias internas, separador de KPI |
| `--line-strong` | `#C5CDD9` | `#31466A` | Hover de botão secundário |
| `--control` | `#7A8597` (3,7:1; 3,4 no canvas, 3,5 no surface-2, 3,3 na linha selecionada) | `#6A7A93` (3,9:1) | Borda de input, checkbox, toggle desligado (3:1 em qualquer fundo claro, não só no surface) |
| `--selection` | `#E6D3A8` | `#16305A` | `::selection` |

> No modo escuro o primário vira "porcelana" com texto marinho, como o botão "Endossar" sobre a face
> da apólice na referência. Marinho sobre fundo noturno some.

### 3.4 Estados (texto · fundo · borda)

| Estado | Claro | Escuro | Contraste do texto | Ícone |
|---|---|---|---|---|
| Sucesso `ok` | `#1E6B47` · `#E6F2EB` · `#BFDCCB` | `#6FD19C` · `#0F2A20` · `#1E4A37` | 5,6 · 8,2 | `CircleCheck` |
| Alerta `warn` | `#8A5700` · `#FBF0D9` · `#EBD3A3` | `#F0BD62` · `#2E2310` · `#574116` | 5,4 · 8,9 | `TriangleAlert` |
| Erro `bad` | `#A3302A` · `#FBE9E7` · `#F0C4BF` | `#F5A39A` · `#3A1A1B` · `#63302D` | 5,9 · 7,9 | `CircleX` / `OctagonAlert` |
| Info `info` | `#24508F` · `#E8EEF8` · `#C5D4EC` | `#8DB4E8` · `#13294A` · `#284A78` | 6,9 · 6,8 | `Info` |
| Neutro `neutral` | `#44526A` · `#EBEEF3` · `#D5DBE4` | `#B4C0D2` · `#1C2B44` · `#2E405E` | 6,8 · 7,7 | `Circle` |

Variantes **sobre marinho** (face da apólice, toasts): ok `#8FE0B5` (10,6:1), warn `#F0C77A` (10,4:1),
bad `#F5A39A` (8,3:1), cada uma com fundo em 14–16% da própria cor e anel interno de 1 px a 25%.

### 3.5 Produtos

A cor de produto aparece **só** em amostra (quadrado 10 px, raio 3), série de gráfico e chip de ícone.
O texto do produto é sempre `--ink`, para não confundir produto com status.

| Produto | Série / amostra (claro · escuro) | Chip de ícone: fundo · ícone (claro · escuro) | Ícone (lucide) |
|---|---|---|---|
| `AUTO` | `#24508F` · `#4F7FC4` | `#E7ECF5` · `#1D3B6B` (9,4:1) / `#13294A` · `#8DB4E8` | `Car` |
| `RESID` | `#5E93CF` · `#B7D2F2` | `#E3EDF8` · `#1F4A85` (7,5:1) / `#152B4A` · `#A9CBF0` | `House` |
| `VIDA` | `#B5862F` · `#D4A84F` | `#F4ECDC` · `#7A5C2C` (5,3:1) / `#2A2416` · `#D9BF8C` | `HeartPulse` |

Todas as séries passam 3:1 contra a superfície (claro: 8,0 · 3,2 · 3,3; escuro: 4,2 · 10,9 · 7,7). Os três
se distinguem por **luminosidade** (escuro, claro, médio-quente), o que sobrevive a daltonismo. No escuro,
AUTO e RESID adjacentes ficam em 2,6:1 entre si; por isso a barra empilhada mantém o vão de 2 px entre segmentos.

### 3.6 Status de domínio → tom

Rótulos e estados vêm do SPEC (§4.3–4.5). O badge sempre mostra ponto/ícone + rótulo.

| Entidade | Status | Tom | Observação |
|---|---|---|---|
| Cotação | Calculada | neutral | |
| | Aceita | info | Pronta para emitir |
| | Emitida | ok | Link para a apólice |
| | Recusada | bad | |
| | Expirada (efetivo) | warn | "Recalcular" como ação; `expira em ≤ 48 h` vira chip warn na lista |
| Apólice | Ativa | ok | |
| | Programada | info | Início futuro |
| | Vencida | neutral | |
| | Cancelada | bad | Mostra motivo e restituição |
| Sinistro | Aberto | info | |
| | EmAnalise ("Em análise") | warn | |
| | Aprovado | ok (ponto) | |
| | Pago | ok (ícone `Check`) | Diferente de Aprovado pelo ícone e pelo rótulo |
| | Negado | bad | Mostra motivo da negativa |
| Parcela (UI) | Próxima | info (borda marinho) | Derivado da data; ver §10.2 sobre pagamento |
| | A vencer | neutral | |
| | Vencimento passado | neutral + ícone relógio | Sem "paga/atrasada" enquanto o domínio não modelar pagamento |

### 3.7 Gráficos

| Token | Claro | Escuro | Uso |
|---|---|---|---|
| `--chart-1` | `#1D3B6B` (11,1:1) | `#6F9BDB` (5,9:1) | Série única (prêmio emitido) |
| `--chart-accent` | `#B5862F` (3,3:1) | `#D4A84F` (7,7:1) | Mês corrente / destaque / projeção |
| `--chart-ref` | `#7A5C2C` | `#D9BF8C` | Linha de média ou meta (tracejada 3 4) + rótulo |
| `--chart-projection` | latão a 14% | `#D4A84F` a 28% | Preenchimento da projeção do mês (no escuro, 14% virava um cinza neutro) |
| `--chart-grid` | `#E9EDF2` | `#1A2A44` | Linhas de grade |
| `--chart-axis` | `#C9D0DA` | `#31466A` | Linha de base |
| `--meter-track` | `#E7EBF1` | `#1A2A44` | Trilho de medidor (sinistralidade, vigência) |

### 3.8 Tipografia

**Famílias (npm, sem Google Fonts em runtime, o que também simplifica a CSP):**

```bash
pnpm add @fontsource-variable/public-sans @fontsource-variable/source-serif-4 @fontsource-variable/geist-mono
```

```css
@import "@fontsource-variable/public-sans";              /* "Public Sans Variable", wght 100–900 */
@import "@fontsource-variable/source-serif-4/opsz.css";  /* "Source Serif 4 Variable", opsz 8–60 + wght */
@import "@fontsource-variable/geist-mono";               /* "Geist Mono Variable", wght 100–900 */
```

| Papel | Família | Onde |
|---|---|---|
| Interface | **Public Sans** (sans neutra e institucional, criada para o USWDS, com algarismos tabulares) | Todo texto de UI, tabelas, formulários, botões |
| Display e números | **Source Serif 4** com eixo óptico (`font-optical-sizing: auto`) | Títulos de página, card e modal; KPIs; números-herói; marca |
| Identificadores | **Geist Mono** (enxerto do C) | `AUTO-2026-000042`, `SIN-…`, `COT-…`, códigos de cobertura (`RCF_DM`), CEP e protocolo em contexto denso |

**Escala** (rem, base 16 px; entre parênteses, mobile < 768 px):

| Token | Família | Tamanho / altura | Peso | Tracking | Uso |
|---|---|---|---|---|---|
| `display-xl` | serif | 2,875rem/1,1 = 46/51 (34/38) | 500 | −0,022em | Manchete do login |
| `display-lg` | serif | 2,5rem/1,05 = 40/42 (28/32) | 500 | −0,02em | Número da apólice na face |
| `title-page` | serif | 2rem/1,15 = 32/37 (27/31) | 600 | −0,02em | H1 de página |
| `title-auth` | serif | 2,125rem/1,15 = 34/39 (28/32) | 600 | −0,02em | "Entrar" |
| `title-modal` | serif | 1,375rem/1,3 = 22/28 | 600 | −0,01em | Título de modal |
| `title-card` | serif | 1,25rem/1,25 = 20/25 (18/23) | 600 | −0,01em | H2 de card |
| `fig-hero` | serif | 2,625rem/1,05 = 42/44 (34/36) | 500 | −0,015em | Prêmio total (cálculo) |
| `fig-kpi` | serif | 2,25rem/1,1 = 36/40 (28/32) | 500 | −0,015em | Valor de KPI |
| `fig-lg` | serif | 1,875rem = 30 · 1,75rem = 28 · 1,5rem = 24 | 500 | −0,015em | Fatos da face, total do gráfico, total de tabela |
| `fig-sm` | serif | 1,1875rem = 19 | 500 | −0,01em | Valor em lista de distribuição |
| `body-lg` | sans | 1rem/1,6 = 16/26 | 400 | 0 | Texto de apoio do login |
| `body` | sans | 0,875rem/1,5 = 14/21 (compacto 13/20) | 400 | 0 | Base de UI e células |
| `body-strong` | sans | 14/21 | 600 | 0 | Nome em lista, título de item |
| `label` | sans | 0,8125rem = 13/18 | 600 (campo) · 500 (KPI) | 0 | Rótulos |
| `meta` | sans | 0,78125rem = 12,5/18 | 400–500 | 0 | Metadados, subtítulos, cabeçalho de tabela (600) |
| `caption` | sans | 0,75rem = 12/16 | 500 | 0 | Ajuda de campo, eixos de gráfico |
| `micro` | sans | 0,71875rem = 11,5/14 | 500–700 | 0 | Rótulos da bottom nav (mínimo absoluto) |
| `mono-id` | mono | 0,92em do contexto | 500 | 0 | IDs e códigos |
| `button` | sans | 14 (lg 15) | 600 | 0 | Botões |

Regras de leitura:
- Texto de conteúdo nunca abaixo de 13 px. 12 px fica para legenda, eixo e ajuda; 11,5 px só na bottom nav.
- Inputs no mobile têm **16 px**, para o iOS não dar zoom no foco.
- Pesos usados: 400, 500, 600 e 700 (700 só em contadores pequenos e na aba ativa mobile).

**Números:**
- Toda célula numérica: `font-variant-numeric: tabular-nums lining-nums`, alinhada à direita.
- Serifa numérica: `font-variant-numeric: lining-nums tabular-nums; font-optical-sizing: auto`.
- **Herói** (até 2 por tela: prêmio total da cotação, prêmio total da face da apólice, total da tabela de
  coberturas): `R$` sobrescrito em sans a 0,42em, cor `--ink-2`; centavos a 0,6em. Nada disso em tabela ou KPI.
- **KPI**: serifa, prefixo `R$` em sans a 0,42em **na linha de base** e sufixo `mil`/`mi` igual.
  Valor abreviado precisa de `title` e `aria-label` com o valor completo ("R$ 286.412,90").
- **Tabela/lista/formulário**: sans tabular, valor completo `R$ 4.528,20`. Nunca usar mono para dinheiro:
  o zero cortado do B foi reprovado.
- Para leitores de tela, o herói fragmentado (`R$` + `4.862` + `,38`) recebe as partes visuais em
  `aria-hidden` e um texto visualmente oculto com o valor inteiro.

**Formatação pt-BR (utilitário único `formatar.ts`):**
- Moeda: `Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })` → `R$ 4.862,38`. O separador
  é NBSP (U+00A0); os testes devem comparar com ` `.
- Compacto: `{ notation: 'compact', maximumFractionDigits: 1 }` → `286,9 mil` / `4,1 mi`.
- Percentual: `7,38%` (alíquota vem do DTO; IOF de VIDA é 0,38%, nunca fixar 7,38%).
- Datas: `dd/mm/aaaa`. **`DateOnly` ("2026-03-15") nunca passa por `new Date(str)`**: isso vira UTC e mostra
  14/03 em Brasília. Faça o parse manual em ano/mês/dia.
- Data e hora: `21/10/2026 · 14:32`. Tempo relativo: `Intl.RelativeTimeFormat('pt-BR')` → "há 12 min",
  com a data absoluta no `title`.
- Mês em eixo: `out`, `nov`… e o ano sob o primeiro mês e sob janeiro.

### 3.9 Espaçamento (base 4 px; escala padrão do Tailwind 4, `--spacing: 0.25rem`)

| Uso | Desktop (≥1280) | Tablet (768–1279) | Mobile (<768) |
|---|---|---|---|
| Padding lateral da página | 40 | 24 | 16 |
| Padding superior da página | 32 | 24 | 20 |
| Vão entre cards / seções | 24 | 24 | 16 |
| Padding de card | 24 (cabeçalho 22 no topo) | 24 | 16 |
| Célula de tabela | 13 × 16 (1ª e última coluna: 24) | idem | vira card (§5) |
| Topbar | 64 de altura, 40 laterais | 64 / 24 | 56 / 8 |
| Sidebar | 248 de largura, padding 22 / 14 / 16 | rail 72 | drawer 300 (máx. 85vw) |
| Item de navegação | 40 de altura, gap 12, padding 12 | 48 × 48 | 44 |
| Bottom nav | — | — | 64 + `env(safe-area-inset-bottom)` |

Largura máxima do conteúdo: 1360 px, centralizada dentro do `main` em telas ≥ 1536.

### 3.10 Raios

| Token | Valor | Uso |
|---|---|---|
| `--radius-xs` | 5 px | Segmento interno, `kbd` |
| `--radius-sm` | 6 px | Ícone de documento, bloco de skeleton |
| `--radius-md` | 8 px | Botões, inputs, selects, itens de navegação, tooltip |
| `--radius-lg` | 10 px | Chips de ícone, bloco da corretora, cartões de escolha, toast |
| `--radius-xl` | 12 px | Cards |
| `--radius-2xl` | 14 px | Face da apólice, modal, bottom sheet (só no topo) |
| `--radius-full` | 999 px | Badges, toggles, avatares, pontos |

### 3.11 Sombras e elevação

A elevação de cards vem **da borda**, não de sombra (decisão do A, confirmada pelo C). Sombra só existe no que flutua.

| Token | Claro | Escuro | Uso |
|---|---|---|---|
| `--shadow-xs` | `0 1px 1px rgb(7 20 39 / .04)` | `none` | Botão secundário |
| `--shadow-thumb` | `0 1px 2px rgb(7 20 39 / .25)` | `0 1px 2px rgb(0 0 0 / .5)` | Bolinha do toggle |
| `--shadow-overlay` | `0 12px 28px -10px rgb(7 20 39 / .28), 0 2px 6px rgb(7 20 39 / .08)` | `0 12px 28px -10px rgb(0 0 0 / .6), 0 2px 6px rgb(0 0 0 / .4)` | Popover, menu, tooltip, toast |
| `--shadow-modal` | `0 24px 56px -16px rgb(7 20 39 / .40), 0 4px 12px rgb(7 20 39 / .10)` | `0 24px 56px -16px rgb(0 0 0 / .7)` | Modal, drawer, sheet |

No escuro, todo elemento flutuante usa `--surface-raised` + borda `--line`: a sombra sozinha não separa nada.

### 3.12 Bordas

Sempre 1 px. `--line` para contorno de card e cabeçalho de tabela, `--line-soft` para divisórias internas,
`--control` para limite de controle de formulário (3:1), `--line-strong` no hover do secundário.
Sobre marinho: fio dourado `rgb(201 168 106 / .28–.38)` e divisória `rgb(255 255 255 / .08)`.
Linha pontilhada (`1px dotted #B9C1CD` / escuro `#31466A`) só como guia de leitura na memória de cálculo.

### 3.13 Densidade

`data-density="comfortable|compact"` no `<html>` (preferência do usuário, padrão confortável; enxerto do C).

| Token | Confortável | Compacto |
|---|---|---|
| `--fs-table` | 14 px | 13 px |
| `--row-y` (padding vertical da célula) | 13 px | 8 px |
| `--row-min` | 52 px (com sublinha) / 44 px | 36 px |
| `--card-pad` | 24 px | 16 px |

O modo compacto vale só para tabelas e listas. Títulos, KPIs e formulários não mudam.

### 3.14 CSS de referência (Tailwind 4)

```css
@import "tailwindcss";
@import "@fontsource-variable/public-sans";
@import "@fontsource-variable/source-serif-4/opsz.css";
@import "@fontsource-variable/geist-mono";

:root { color-scheme: light dark; }
:root[data-theme="light"] { color-scheme: light; }
:root[data-theme="dark"]  { color-scheme: dark; }

:root {
  --canvas: light-dark(#F2F4F8, #0A1528);
  --surface: light-dark(#FFFFFF, #0F1D33);
  --surface-2: light-dark(#F7F8FB, #14243E);
  --surface-raised: light-dark(#FFFFFF, #15263F);
  --surface-hover: light-dark(#FAFBFD, #13223B);
  --surface-selected: light-dark(#EEF2F9, #16263F);
  --sidebar: light-dark(#0B1F3A, #060E1C);
  --sidebar-active: light-dark(#16305A, #13243F);
  --brand-surface: #0B1F3A;
  --ink: light-dark(#0E1A2C, #E8EDF5);
  --ink-2: light-dark(#44526A, #B4C0D2);
  --ink-3: light-dark(#5D697D, #8E9CB3);
  --on-navy: #F4F6FA; --on-navy-2: #B8C3D6; --on-navy-3: #8F9DB5;
  --gold-text: light-dark(#7A5C2C, #D9BF8C);
  --gold-on-navy: #D9BF8C; --gold-line: #C9A86A;
  --link: light-dark(#24508F, #8DB4E8);
  --focus: light-dark(#24508F, #8DB4E8);
  --primary-bg: light-dark(#0B1F3A, #DCE4F0);
  --primary-bg-hover: light-dark(#16305A, #FFFFFF);
  --primary-fg: light-dark(#FFFFFF, #0B1F3A);
  --danger-bg: light-dark(#A3302A, #B4382F);
  --danger-bg-hover: light-dark(#8C2823, #C4443A);
  --line: light-dark(#DFE4EC, #233552);
  --line-soft: light-dark(#ECEFF4, #1A2A44);
  --line-strong: light-dark(#C5CDD9, #31466A);
  --control: light-dark(#7A8597, #6A7A93);
  --ok: light-dark(#1E6B47, #6FD19C);    --ok-bg: light-dark(#E6F2EB, #0F2A20);
  --warn: light-dark(#8A5700, #F0BD62);  --warn-bg: light-dark(#FBF0D9, #2E2310);
  --bad: light-dark(#A3302A, #F5A39A);   --bad-bg: light-dark(#FBE9E7, #3A1A1B);
  --info: light-dark(#24508F, #8DB4E8);  --info-bg: light-dark(#E8EEF8, #13294A);
  --neutral: light-dark(#44526A, #B4C0D2); --neutral-bg: light-dark(#EBEEF3, #1C2B44);
  --prod-auto: light-dark(#24508F, #4F7FC4);
  --prod-resid: light-dark(#5E93CF, #B7D2F2);
  --prod-vida: light-dark(#B5862F, #D4A84F);
  --chart-1: light-dark(#1D3B6B, #6F9BDB);
  --chart-accent: light-dark(#B5862F, #D4A84F);
  --chart-ref: light-dark(#7A5C2C, #D9BF8C);
  --ease-out: cubic-bezier(.2, .8, .2, 1);
  --ease-in: cubic-bezier(.4, 0, 1, 1);
  --ease-in-out: cubic-bezier(.65, 0, .35, 1);
}

@theme inline {
  --font-sans: "Public Sans Variable", system-ui, sans-serif;
  --font-serif: "Source Serif 4 Variable", Georgia, serif;
  --font-mono: "Geist Mono Variable", ui-monospace, monospace;
  --color-canvas: var(--canvas);
  --color-surface: var(--surface);
  --color-surface-2: var(--surface-2);
  --color-ink: var(--ink);
  --color-ink-2: var(--ink-2);
  --color-ink-3: var(--ink-3);
  --color-line: var(--line);
  --color-line-soft: var(--line-soft);
  --color-link: var(--link);
  --color-ok: var(--ok);   --color-ok-bg: var(--ok-bg);
  --color-warn: var(--warn); --color-warn-bg: var(--warn-bg);
  --color-bad: var(--bad); --color-bad-bg: var(--bad-bg);
  --color-info: var(--info); --color-info-bg: var(--info-bg);
  /* …demais tokens no mesmo padrão */
  --radius-md: 8px; --radius-lg: 10px; --radius-xl: 12px; --radius-2xl: 14px;
}

body { background: var(--canvas); color: var(--ink); font-family: var(--font-sans); font-size: .875rem; line-height: 1.5; }
::selection { background: light-dark(#E6D3A8, #16305A); }
:focus-visible { outline: 2px solid var(--focus); outline-offset: 2px; }
.on-navy :focus-visible { outline-color: var(--gold-line); }
```

---

## 4. Componentes

Biblioteca de ícones: `lucide-react` com imports nomeados, `strokeWidth={1.6}`, tamanhos 18 (navegação e
botões), 15 (inline) e 13 (micro). Primitivos acessíveis: Radix (Dialog, Tabs, Switch, Select, Tooltip,
Toast, DropdownMenu, RadioGroup, ToggleGroup).

### 4.1 Marca e gravura
- **Marca:** selo de 30 px (círculo r15 + roseta guilloché de 8 lóbulos + círculo interno r3), traço com
  gradiente "folha dourada" (`#8E6E3C → #D9BF8C → #A9884F`), mais a palavra "Apólice" em serifa 600 de 22 px.
- **Gravura:** rosetas paramétricas (função `ring` da referência), traço `--gold-line` de 0,6–0,7 px,
  opacidade 0,22–0,30, `aria-hidden`. Gere os SVGs **em build** (script Node + SVGO) e importe como
  arquivo: a versão XL tem cerca de 40 mil pontos e não deve ser calculada no runtime.
- Uso permitido: painel do login (xl), face da apólice (lg), bloco do prêmio total (sm) e empty state
  (anel fino a 0,18). Nunca sobre superfície branca, nunca mais de uma por viewport.

### 4.2 Sidebar
- **Anatomia (de cima para baixo):** marca → bloco da corretora (borda de fio dourado a 28%, nome 13,5/600,
  "Corretora fictícia · demonstração" 12; estático, sem chevron nem menu, porque o SPEC não modela corretora,
  ver §10.2) → navegação → separador → **Filas salvas** (enxerto do C) → "Sobre o projeto" →
  rodapé do usuário (avatar, nome, papel, botão ⇄ "Trocar persona").
- **Item de navegação:** 40 px, raio 8, ícone 18 em `--on-navy-3`, rótulo 14/500 em `--on-navy-2`,
  contador à direita (12/600 tabular). Hover: fundo branco a 5%. Ativo: `--sidebar-active`, texto branco
  600, ícone `--gold-line`, `aria-current="page"`. Restrito (Admin para não-admin): cadeado no lugar do
  contador, com `aria-label="Restrito ao perfil Admin"`.
- **Filas salvas:** rótulo de grupo "Filas salvas" 12/600 em `--on-navy-3` (grupo rotulado dentro do nav, não
  `section`/`h2`); itens de 40 px (44 no drawer do mobile) com ponto de 6 px no tom do estado, rótulo 13,5 e
  contador (12/600 tabular, quando a API informar). Por persona, com dados que o SPEC tem:
  - Corretor: "Cotações expirando (48 h)" (warn), "Renovações em 30 dias" (info), "Aceitas a emitir" (info).
  - Analista: "Sinistros abertos" (info), "Em análise" (warn).
  - Cada fila é um link para a lista com o filtro aplicado na URL.
- `<nav aria-label="Principal">`, links reais (`<a>`), nunca `div` clicável.

### 4.3 Topbar
- 64 px, `--surface`, borda inferior `--line`. À esquerda, o **gatilho de busca** (420 px, raio 8, ícone,
  "Buscar apólice, segurado, CPF ou placa", `kbd` "⌘K"/"Ctrl K"). Ele é um botão que abre a **paleta de
  comandos** (Dialog + combobox), não um input.
- À direita: chip "Demonstração" (`--gold-text` sobre `#F6EFE1`, 5,4:1; escuro `#D9BF8C` sobre `#2A2416`),
  ajuda, notificações (ponto `--bad` + `aria-label="Notificações, 3 não lidas"` só quando há não lidas; sem
  API, painel vazio honesto) e menu de **Aparência**, com tema (Sistema/Claro/Escuro em radio) e densidade.
- **Decisão:** no desktop a identidade do usuário (avatar, nome, papel, ⇄ "Trocar persona") fica só no rodapé
  da sidebar/rail, como na referência; a topbar não repete um segundo avatar e o "menu do usuário" é o menu de
  Aparência. No mobile, o avatar da top bar troca a persona e Tema/Densidade ficam no drawer (§5.2).
- Na página de detalhe, o título não se repete na topbar: o breadcrumb fica no conteúdo, acima do H1.

### 4.4 Cartão KPI (faixa)
- Um card com até 6 KPIs em grade, separados por fio vertical `--line-soft` inset de 20 px (topo e base).
- KPI: rótulo 13/500 `--ink-2` → valor `fig-kpi` → meta 12,5 `--ink-3` com delta (`↑ 38` em `--ok` 600, ou
  número de atenção em `--warn` 600 seguido de texto: "**12** expiram em 48 h").
- **Medidor** (sinistralidade, meta): trilho 4 px `--meter-track`, preenchimento `--link`, marcador de meta
  2 × 12 px em **`--gold-text`** (correção: `#B08D57` dava 2,6:1) e texto "Meta até 60%". Acima da meta, o
  preenchimento vira `--warn` e o texto ganha "acima da meta".
- O KPI inteiro é link para a lista filtrada. Foco com anel no card do KPI.
- Valores pequenos (o seed tem cerca de 10 apólices) precisam ficar bonitos: nada de layout que dependa de 4 dígitos.

### 4.5 Tabela
- Dentro de card. Cabeçalho `--surface-2`, 12,5/600 `--ink-2`, bordas `--line` em cima e embaixo. Células
  `--fs-table`, divisória `--line-soft`, padding `--row-y` × 16 (primeira e última coluna com 24).
- Números à direita e tabulares; código e ID em `mono-id`; sublinha descritiva 12,5 `--ink-3`; vazio "—".
- Hover `--surface-hover`. Selecionada `--surface-selected` + barra interna de 2 px `--link` à esquerda.
- Ordenação: o cabeçalho é `<button>` com ícone e `aria-sort`. A linha navega por um `<a>` real na célula do
  ID (a linha inteira pode ter hover, mas o alvo acessível é o link).
- Rodapé de totais (`tfoot`): rótulos `--ink-2`; total em `fig-lg` 24, que é herói só na tabela de coberturas.
- **Barra de ferramentas:** busca (debounce 300 ms), filtros de status/produto como chips, densidade e
  exportar. Paginação por cursor ("Mostrando 25 de 1.284" + "Carregar mais"), coerente com o SPEC.
- Cabeçalho fixo dentro do card quando houver rolagem. Abaixo de 768 px, cada linha vira card (§5.3).

### 4.6 Badge de status
- 24 px, pílula, padding 0 10, 12,5/600, ponto de 6 px (`::before`) ou ícone de 13 px, fundo `*-bg` e
  texto no tom do estado (tabela §3.4). Variante `plain` sem ponto e variante `on-navy` (§3.4).
- **Badge contextual** (enxerto do B): ao lado do status na face da apólice, por exemplo
  "⚠ 1 sinistro em análise" (warn on-navy), que leva ao sinistro.
- **Chip de prazo:** "expira em 2 dias" / "vence em 25 dias" em warn ou neutral, sempre com o número.

### 4.7 Botões
| Variante | Visual | Uso |
|---|---|---|
| Primário | `--primary-bg`, texto `--primary-fg`, 600 | Uma ação principal por região ("Nova cotação", "Revisar cotação", "Emitir apólice") |
| Secundário | `--surface`, borda `--line`, `--shadow-xs`; hover `--line-strong` + `--surface-2` | "Exportar", "Salvar rascunho" |
| Fantasma | sem fundo, `--ink-2`; hover fundo 5% | "Voltar para Risco", ações de toolbar |
| Perigo (sólido) | `--danger-bg`, texto branco | Confirmação dentro do modal ("Cancelar apólice") |
| Perigo (contorno) | borda `--bad` a 45%, texto `--bad`, ícone `Ban` | Gatilho de ação destrutiva fora do modal |
| Sobre marinho | contorno branco a 22% (`--on-navy`); "claro" = fundo branco + texto marinho; perigo = contorno `#F5A39A` a 45% + texto `#F5A39A` | Ações da face da apólice |

- Alturas 40 (padrão), 46 (lg: login, CTA mobile) e 32 (sm: tabelas). Raio 8, gap 8, ícone 18.
- Hover 120 ms; `:active` com `scale(.98)`; desabilitado com fundo `--surface-2`, texto `--ink-3` e `cursor: not-allowed`.
- Carregando: spinner de 16 px no lugar do ícone, rótulo no gerúndio ("Emitindo…"), largura travada,
  `aria-busy`, clique bloqueado. Submit desabilitado durante o processamento.
- **Correção do A:** "Cancelar" deixa de ser link coral e vira **perigo em contorno sobre marinho**, abrindo o
  modal de confirmação com simulação (§4.17).

### 4.8 Input, select e campo
- Altura 44 (formulário) / 40 (inline na cotação), raio 8, borda `--control`, fundo `--surface`, padding 0 14,
  texto 15 (16 no mobile). Rótulo 13/600 acima; ajuda 12 `--ink-3` abaixo.
- **Prefixo** interno em `--ink-3` ("R$", "LMI R$"), enxerto do C. Máscaras: CPF `000.000.000-00`,
  CEP `00000-000`, moeda pt-BR com `inputmode="decimal"`.
- **Faixa válida** na ajuda (enxerto do C), vinda de `LmiMinimo`/`LmiMaximo`: "Entre R$ 50.000,00 e R$ 500.000,00".
- **Regra explicada no campo** (enxerto do C), na voz do negócio: "IPA não pode passar o LMI de Morte
  (R$ 200.000,00)", "Casco segue o valor do veículo (R$ 141.900,00)", "Carro reserva não permite sinistro".
- Foco: borda `--focus` + anel de 3 px `rgb(36 80 143 / .18)` (escuro `rgb(141 180 232 / .25)`).
- Erro: borda `--bad`, mensagem 12,5 `--bad` com ícone, `aria-invalid` e `aria-describedby`. Validação com
  Zod, debounce 300 ms, nunca no primeiro foco.
- Desabilitado: fundo `--surface-2`, borda `#C9D0DA` / `#2A3B57`, texto `--ink-3`.
- Select: Radix Select, mesma caixa, chevron `ChevronsUpDown` 16.

### 4.9 Toggle (Switch)
- Radix Switch (`role="switch"`), trilho 40 × 24, bolinha 18 com `--shadow-thumb`, deslocamento de 16 px em 220 ms `--ease-out`.
- Ligado: trilho `--primary-bg` (escuro: `#8DB4E8` com bolinha `#0A1528`). Desligado: trilho `--control` com bolinha branca.
- **Obrigatória** (enxerto do B/C): ligada e desabilitada, com badge neutral "Obrigatória" e cadeado ao lado do nome.
- A linha da cobertura desligada mantém o preço em `--ink-3` como "+ R$ 38,00 se ativada", em vez de zerar.

### 4.10 Controle segmentado e cartões de escolha
- **Segmentado** (`ToggleGroup` com semântica de radiogroup): borda `--control`, padding 3, gap 3, altura 40,
  segmento interno com raio 5, ativo `--primary-bg`/`--primary-fg` 600. Navegação por setas.
  Usos: parcelamento (enxerto do C, por exemplo 1× · 3× · 6× · 10×, filtrado por `MaxParcelas` + "Outro"),
  Tipo de imóvel (Casa/Apartamento), Uso (Particular/Comercial), filtro de período do dashboard.
- **Cartões de escolha** (enxerto do B, a escolha da franquia em 3 cards): `RadioGroup` com cards de raio 10,
  borda `--line`; o selecionado tem borda interna de 2 px `--primary-bg` + check. Cada card mostra título,
  efeito em R$ e fator. Opção indisponível fica desabilitada **com o motivo no próprio card**.
  Usos: escolha do produto na etapa 1 (ícone, nome, faixa de LMI) e qualquer escolha de 2 a 4 opções com preço.

### 4.11 Stepper (assistente de cotação)
- `<ol>` em card, com 5 etapas: Produto · Segurado · Risco · Coberturas · Revisão.
- Cada etapa tem barra superior de 3 px (concluída `--chart-1`, atual `--gold-line`, futura `--line`),
  círculo de 28 px (concluída: marinho com check; atual: anel de 2 px marinho com número; futura: anel
  `--control`), título 14/600 e resumo 12,5 do que foi preenchido ("Lucas Andrade · 22 anos").
- A etapa atual mostra "Em edição" em `--gold-text` e recebe `aria-current="step"`. Etapas concluídas são
  links (voltar e editar); futuras não são clicáveis.
- Indicador de autosave à direita do título: "✓ Rascunho salvo às 14:36" (`aria-live="polite"`, discreto).
- Mobile: vira "Etapa 4 de 5 · Coberturas" + barra segmentada de 5 partes; a lista completa abre num menu.

### 4.12 Painel de cálculo (memória de cálculo ao vivo)
Card fixo (`position: sticky; top: 24px`) de 376 px à direita do formulário. Ele **reflete o modelo do SPEC
§4.2**: os fatores multiplicam só os itens com taxa (prêmio fixo não sofre fator) e o prêmio líquido é a
soma dos itens.
1. **Cabeçalho:** "Cálculo do prêmio" (`title-card`), "Tarifa Auto · referência dd/mm/aaaa" e indicador
   "● Ao vivo · 14:36:08" (`--ok`, ponto com halo).
2. **Itens:** uma linha por cobertura com guia pontilhada (rótulo ···· valor), tabular. Item de prêmio fixo
   leva "fixo" em `--ink-3`.
3. **Fatores aplicados** (enxerto do B, junto com a frase explicativa do A): chip
   `↗ ×1,40` (agrava, `--warn`) ou `↘ ×0,91` (desconta, `--ok`), em sans 600 tabular com seta. Ao lado vão a
   descrição de negócio ("Condutor de 18 a 25 anos", "Região 4 · BA/SE", "Bônus classe 3") e
   "sobre Casco, RCF-DM e RCF-DC". Efeito em R$ por fator (`+R$ 968,12`) só quando calculável no cliente
   (taxa do catálogo × LMI, aplicado em sequência na ordem exibida, com o último fator fechando o
   arredondamento), sob o rótulo "efeito estimado".
4. **Subtotais:** Prêmio líquido; IOF com a alíquota do DTO ("IOF 7,38%" ou "IOF 0,38%").
5. **Bloco do total** (`--brand-surface`, gravura sm): "Prêmio total" `--on-navy-2`, valor `fig-hero` branco
   com `R$` sobrescrito e centavos reduzidos, linha de parcelamento em `--gold-on-navy`
   ("ou até 10× de R$ 458,11 sem juros"), validade ("Cotação válida até 20/11/2026") separada por fio.
6. **Parcelamento** (enxerto do C): segmentado + "1ª parcela absorve o ajuste de centavos" (regra do SPEC,
   texto do B) + CTA primário "Revisar cotação".

Comportamento: simulação com debounce de 300 ms (`POST /cotacoes/simular`). Durante o recálculo, os
valores ficam a 60% de opacidade, sem mudança de layout. **Só os valores em `--ink`/`--on-navy` esmaecem**
(a 60% ainda dão 4,6:1 e 6,3:1); rótulos, notas "fixo", chips de fator e a linha de parcelamento em
`--gold-on-navy` ficam plenos, porque esmaecidos cairiam abaixo de 4,5:1. O `aria-busy` marca o painel. A linha que mudou recebe um realce
(`--selection` a 40%, apagando em 900 ms). Uma região `aria-live="polite"` anuncia só o total, com debounce
("Prêmio total atualizado: R$ 4.581,08"). Se a simulação falhar, o último valor válido continua visível
com "Desatualizado · Tentar de novo".

### 4.13 Face da apólice (certificado)
- Card `--brand-surface`, raio 14, faixa de folha dourada de 6 px no topo (com o traço da marca, os únicos gradientes do sistema) e
  gravura lg no canto superior direito. No escuro, ganha também o fio dourado a 30% em volta.
- **Topo:** linha de produto (ícone + "Seguro Auto" em `--gold-on-navy` · "Produto AUTO · emitida em
  15/03/2026" em `--on-navy-3`) → número em `display-lg` serifa → badge de status on-navy + **badge
  contextual** → "Segurado **Nome** · CPF ***.482.917-**" e dados do risco.
- **Ações:** "Baixar PDF" (sobre marinho), "Endossar" (claro), "Cancelar" (perigo em contorno sobre
  marinho). No mobile, ficam "Endossar" + menu ⋯ com o resto.
- **Faixa de fatos** (fundo `#091A31`, fio dourado em cima, células divididas por branco a 8%): Vigência
  (datas + barra de progresso `--gold-line` + "220 de 365 dias · renova em 145 dias"), **Prêmio total**
  (herói, `fig-lg` 30), Parcelamento e LMI principal.
- Abaixo da face, **abas com contadores** (enxerto do C/B): Visão geral · Coberturas 6 · Parcelas 10 ·
  Endossos 1 · Sinistros 1 · Documentos · Auditoria. São Radix Tabs com sublinhado de 2 px `--primary-bg`,
  contador em pílula neutral e setas para navegar. No mobile, a lista de abas rola na horizontal dentro
  dela mesma, com máscara de fade.
- **Registro** (enxerto do C) no rodapé da coluna lateral: selo gravado pequeno + "Registro SRO ·
  simulado · protocolo `2026.0315.0004211`". O rótulo "simulado" é obrigatório, porque é um projeto de portfólio.

### 4.14 Linha do tempo
- Itens em grade 20 px + conteúdo, com linha vertical de 1 px `--line` entre os nós.
- O nó tem 20 px, com fundo `*-bg` e ponto de 8 px no tom do evento: sinistro warn, endosso dourado,
  notificação aço, emissão marinho, cancelamento bad.
- Data e hora 12,5 tabular `--ink-3` (absoluta; relativa no `title`), título 14/600 com ID em `mono-id` link,
  descrição 13 `--ink-2` e badge opcional.
- Marcador **"Hoje"** (enxerto do B) quando a linha mistura passado e futuro (vigência, parcelas).
- Mais recente no topo. O link "Auditoria" leva à trilha completa. A mesma linha do tempo serve ao
  histórico do sinistro (`HistoricoSinistro`).

### 4.15 Gráfico (Recharts)
- Card com título em serifa, subtítulo "Últimos 12 meses fechados · em R$ mil" e total-herói à direita
  ("R$ 4,08 mi" + "+14,2% vs. 12 meses anteriores").
- Barras: largura 26 (mobile 14), raio superior 4, `--chart-1`; mês corrente em `--chart-accent`.
- **Projeção do mês** (enxerto do C): a parte realizada é sólida e o restante projetado tem contorno
  tracejado `--chart-accent` com preenchimento `--chart-projection` (14% no claro, 28% no escuro).
- Média tracejada `--chart-ref` com rótulo "Média 340" **fora da área das barras**: no desktop, à direita do
  plot (espaço reservado na margem); no mobile o rótulo sai do plot e o valor vai para a legenda
  ("Média mensal · 340"). Rótulo sobre barra, mesmo com halo, é proibido como o tooltip estático (§10.1).
- **Legenda explícita** no topo: Emitido · Projeção do mês · Média 12 m.
- Grade `--chart-grid` com 4 marcas; eixos 12 `--ink-3`; ano sob o primeiro mês e sob janeiro.
- **Tooltip só em hover/foco** (correção: nada estático cobrindo barras): `--brand-surface`, raio 8,
  `--shadow-overlay`, linha 1 "set/2026 · 96 apólices" em `--on-navy-2`, valor em serifa 17 branco e, no
  mês corrente, "parcial até 21/10".
- **"Ver como tabela"** (enxerto do B): alterna para `<table>` com `caption` e os mesmos dados.
- Ativar a camada de acessibilidade do Recharts (`accessibilityLayer`, navegação por setas) e
  `isAnimationActive={!reducedMotion}`. Mês sem dado mostra um traço de 2 px na base, sem buraco.
- Distribuição por produto: barra empilhada de 12 px com vão de 2 px e raio 4 nas pontas
  (`role="img"` + `aria-label` completo), seguida de lista com amostra, nome, "742 apólices · 58%" e valor
  `fig-sm`, e rodapé com ticket médio por produto.

### 4.16 Estado vazio
- Centralizado no card, largura máxima de 420 px: chip de ícone de 48 px (raio 12, `--surface-2`, borda
  `--line`, ícone 22 `--ink-2`) com anel de gravura a 0,18 → título em serifa 20/600 → texto 14 `--ink-2` →
  ação primária e link secundário.
- Variantes:
  - **Primeiro uso:** "Nenhuma cotação ainda" + "Nova cotação".
  - **Sem resultado:** "Nada encontrado para "RFN3E47"" + "Limpar filtros".
  - **Sem permissão:** "Disponível para o perfil Admin", com cadeado.
  - **Erro:** "Não foi possível carregar as apólices" + "Tentar de novo" + "ID de correlação" em
    `mono-id` com botão copiar (vem do ProblemDetails, §8 do SPEC).

### 4.17 Modal (Radix Dialog)
- Overlay `rgb(7 20 39 / .55)` (escuro `rgb(0 0 0 / .6)`). Contêiner `--surface-raised`, raio 14,
  `--shadow-modal`; larguras 440 (confirmação), 560 (formulário) e 720 (simulação).
- Cabeçalho com padding 24, título `title-modal` e descrição 14 `--ink-2`. Rodapé `--surface-2` com borda
  superior e ações à direita (secundária, depois a principal).
- Foco preso, `Esc` fecha e o foco volta ao gatilho. O foco inicial vai para o **primeiro campo ou para a
  ação segura**, nunca para a destrutiva.
- **Cancelar apólice:**
  - motivo (RadioGroup com os 3 motivos do SPEC);
  - simulação ao vivo: método (Prazo curto / Pro rata die / Integral), dias decorridos, % retido e
    restituição como herói;
  - nota "O IOF não é restituído";
  - botão perigo sólido "Cancelar apólice", desabilitado até escolher o motivo.
- **Endosso:** mesma estrutura, com simulação de `AjusteLiquido` + `IofAjuste`.
- Mobile: vira bottom sheet (raio 14 só no topo, alça de 36 × 4 px), ações empilhadas em largura total,
  com a principal em cima.

### 4.18 Toast (Radix Toast)
- Superfície invertida: `--brand-surface` (escuro: `--surface-raised` + borda `--line`), raio 10,
  `--shadow-overlay`, largura 380 (mobile: largura total menos 32).
- Ícone no tom on-navy, título 14/600 `--on-navy`, descrição 13 `--on-navy-2` e ação em `--gold-on-navy`
  ("Ver apólice", "Desfazer"), além do botão fechar.
- Posição: canto inferior direito, a 24 px. No mobile, acima da bottom nav (64 + 12). No máximo 3 empilhados.
- Duração: sucesso e info 5 s, alerta 7 s, **erro persistente** até fechar, com ID de correlação copiável.
  Pausa no hover/foco. Região `aria-live`: `polite` normalmente, `assertive` em erro.

### 4.19 Skeleton
- Blocos `--line-soft` (escuro `#1A2A44`), raio 6, **com as mesmas dimensões do conteúdo final** (CLS zero).
  Linhas de texto com 10–12 px de altura e larguras de 60–80%.
- Rótulos estáticos (nome do KPI, cabeçalho de tabela) aparecem de verdade; só os valores viram skeleton.
- Tabela com 5 linhas fantasma; gráfico com eixos reais e 12 barras de alturas suaves.
- Aparece só após 200 ms, para não piscar. Pulsa de opacidade 1 a 0,6 em 1,4 s, sem shimmer de gradiente.
- `aria-busy="true"` na região, com "Carregando…" visualmente oculto.

---

## 5. Layout responsivo

### 5.1 Breakpoints (Tailwind 4 padrão)

| Faixa | Largura | Shell |
|---|---|---|
| base | < 768 | Top bar marinho de 56 px (☰, marca, busca, sino, avatar) + **bottom nav** (Início, Cotações, Apólices, Sinistros, Mais) |
| `md` | 768–1023 | **Rail** marinho de 72 px (ícones de 48 × 48 com tooltip "Cotações · 57") + topbar 64; o ☰ do rail expande a sidebar completa em overlay |
| `lg` | 1024–1279 | Rail de 72 px; o painel de cálculo e a coluna lateral do detalhe passam para o lado |
| `xl` | ≥ 1280 | Sidebar completa de 248 px |
| `2xl` | ≥ 1536 | Conteúdo limitado a 1360 px e centralizado |

### 5.2 Sidebar no mobile
- O ☰ abre um **drawer** (Radix Dialog) que entra pela esquerda: largura 300 (máx. 85vw), mesma anatomia
  da sidebar (corretora, navegação, Filas salvas, Sobre, usuário) e, no rodapé, Tema e Densidade.
  Tem foco preso, `Esc`/overlay fecham e o foco volta ao ☰.
- Bottom nav: 64 px + safe area, fundo `--surface`, borda superior `--line`. Cada item tem ícone 20 + rótulo
  `micro` e alvo de 44 px. O ativo fica em `--ink` 700 e ícone marinho. "Mais" abre o drawer.
- O conteúdo ganha `padding-bottom` de 64 + safe area. Na cotação mobile, a bottom nav dá lugar à barra do total.

### 5.3 Páginas
- **Dashboard:**
  - KPIs: 2 colunas (base), 3 (`md`), 6 (`xl`).
  - Grade de 12 colunas a partir de `lg`: gráfico 8 + distribuição 4; atividade 8 + renovações 4.
  - Ordem no mobile: "Nova cotação" (lg, largura total) → KPIs → gráfico → distribuição → **renovações**
    (acréscimo) → atividade.
  - Atividade (enxerto do C/B), no desktop: tabela Evento + ID mono · Detalhe · Responsável (avatar) · Quando
    (relativo), com abas de filtro Tudo · Emissões · Sinistros · Cotações. No mobile: lista de 2 linhas do A.
- **Detalhe da apólice:**
  - Breadcrumb → face → abas → grade `1fr 380px` (`lg+`).
  - Abaixo de `lg`, a coluna lateral (linha do tempo, partes, documentos, registro) desce para depois do
    conteúdo principal.
  - Coberturas: tabela Código (mono) · Cobertura · LMI · Prêmio, com tfoot de totais.
  - Parcelas: **tabela** (enxerto do C) Parcela · Vencimento · Valor · Situação, com marcador "Hoje" e nota
    "1ª parcela absorve o ajuste de centavos".
- **Cotação:**
  - Grade `1fr 376px` (`lg+`).
  - Abaixo de `lg`, o painel de cálculo vira **barra fixa inferior** de 64 px ("Prêmio total R$ 4.581,08 ·
    Ver cálculo"), que abre um bottom sheet com a memória completa.
  - As linhas de cobertura empilham: toggle + nome; depois controle; depois preço.
- **Listas** (cotações, apólices, sinistros): tabela no `md+`. No mobile, cada linha vira card:
  - ID mono + badge na 1ª linha;
  - segurado 14/600 na 2ª;
  - produto + valor + data na 3ª.
- **Login:**
  - Painel de marca de 620 px (`xl`) ou 480 px (`lg`).
  - Abaixo de `md`, o painel vira faixa de 200 px com marca e manchete de 28 px (sem o cartão-espécime) e
    o formulário ocupa a largura com gutter de 16.
  - Personas em lista com seta (A).
- **Regra global:** a página nunca rola na horizontal. Tabela larga demais fica em contêiner com rolagem
  própria e 1ª coluna fixa. Em 200% de zoom num notebook de 1280 px (viewport de 640 CSS px), vale o layout base.

---

## 6. Motion

| Token | Valor | Uso |
|---|---|---|
| `--dur-fast` | 120 ms | Hover, cor, fundo de linha |
| `--dur-base` | 180 ms | Sublinhado de aba (desliza), segmentado, pressão |
| `--dur-toggle` | 220 ms | Bolinha do switch |
| `--dur-enter` | 220 ms | Popover, menu, tooltip, toast (entrada) |
| `--dur-panel` | 280 ms | Modal, drawer, bottom sheet (entrada) |
| saída | ~75% da entrada | Saídas sempre mais rápidas |
| `--ease-out` | `cubic-bezier(.2,.8,.2,1)` | Entradas e mudanças de estado |
| `--ease-in` | `cubic-bezier(.4,0,1,1)` | Saídas |
| `--ease-in-out` | `cubic-bezier(.65,0,.35,1)` | Movimento dentro da tela (indicador de aba) |

Catálogo:
- **Popover/menu:** opacidade 0→1 + `translateY(4px)`→0 + `scale(.98)`→1 a partir da origem do Radix.
- **Modal:** overlay em 200 ms; conteúdo com opacidade + `scale(.97)`→1.
- **Drawer e sheet:** translate de borda a borda.
- **Toast:** entra subindo 12 px; dá para dispensar arrastando.
- **Gráfico:** barras crescem só na **primeira** montagem (400 ms, sem stagger acima de 240 ms no total);
  atualização de dado faz tween de 200 ms.
- **Cálculo:** troca de valor com crossfade de 160 ms e realce da linha alterada (§4.12).
- **Sem movimento:** contagem de números, troca de página, parallax e bounce. Só `transform` e `opacity`
  animam; nunca `width`/`height`/`top`.

**`prefers-reduced-motion: reduce`:** remove todo `transform` animado (drawer e modal só fazem fade de
120 ms), desliga o crescimento das barras e o pulso do skeleton, e troca o crossfade do cálculo pela troca
direta (o realce de cor da linha continua, porque não é movimento). No JS, um hook
`useReducedMotion()` alimenta o Recharts e qualquer animação programática.

---

## 7. Acessibilidade (WCAG 2.1 AA)

**Contraste verificado** (calculado pela fórmula WCAG; amostra dos pares críticos):

| Par | Claro | Escuro |
|---|---|---|
| Texto principal / superfície | 17,5:1 | 14,4:1 |
| Texto secundário / superfície | 7,9:1 | 9,2:1 |
| Metadado (`--ink-3`) no pior fundo | 4,9:1 (linha selecionada) · 5,0:1 (canvas) | 5,5:1 (surface-2) |
| Link / canvas | 7,3:1 | 8,5:1 |
| Texto dourado / superfície | 6,2:1 | 9,5:1 |
| Primário (texto/fundo) | 16,5:1 | 12,9:1 |
| Perigo (branco/fundo, hover) | 7,0 · 8,6 | 5,9 · 5,0 |
| Estados (ok, warn, bad, info, neutral) | 5,6 · 5,4 · 5,9 · 6,9 · 6,8 | 8,2 · 8,9 · 7,9 · 6,8 · 7,7 |
| Sobre marinho (`--on-navy-3`, dourado, cancelar) | 6,0 · 9,3 · 8,3 | idem |
| Borda de controle (3:1) | 3,7:1 (3,3 no pior fundo, a linha selecionada) | 3,9:1 (3,5 no pior fundo) |
| Séries de gráfico (3:1) | 8,0 · 3,2 · 3,3 | 4,2 · 10,9 · 7,7 |
| Foco sobre marinho (dourado) | 7,3:1 | 8,6:1 (sidebar escura) |

Regras:
- **Foco visível sempre:** `outline` de 2 px `--focus` com offset 2; sobre marinho, `--gold-line`
  (o azul de foco sobre marinho dá 2,1:1 e é proibido). Inputs usam borda + anel. Nunca `outline: none`
  sem substituto.
- **Teclado:**
  - "Pular para o conteúdo" é o primeiro link da página.
  - Ordem de tabulação segue a leitura.
  - `⌘K`/`Ctrl K` abre a paleta.
  - Abas, segmentados e radios usam setas; o switch usa espaço.
  - Modais e drawers prendem o foco e devolvem ao gatilho.
- **Semântica:**
  - Landmarks: `header`, `nav` (com rótulo), `main`, `aside` na coluna lateral.
  - Hierarquia de títulos: H1 da página → H2 dos cards.
  - `<html lang="pt-BR">`.
  - Tabela com `<th scope>` e `caption`, que pode ser visualmente oculta.
- **Cor nunca sozinha:** status com ponto ou ícone + rótulo; delta com seta + sinal; medidor com texto;
  fila com rótulo.
- **Anúncios:** autosave e total da cotação via `aria-live="polite"`; erros via toast `assertive` +
  mensagem no campo.
- **Alvos:** 40 px no desktop, 44 px no mobile.
- **Zoom 200% e reflow a 320 px** sem perda (vale o layout base). Respeitar `prefers-color-scheme` e
  `prefers-reduced-motion`.
- **Testes:**
  - `vitest-axe` em cada componente (claro e escuro).
  - Playwright + `@axe-core/playwright` nas páginas, em 375 e 1440 px e nos dois temas.
  - Teste de teclado nos fluxos de cotação → emissão e cancelamento.

---

## 8. Conteúdo e voz (pt-BR)

- Português de negócio de seguros: "prêmio", "LMI", "vigência", "endosso", "regulação", "restituição".
  Sem anglicismo de SaaS ("onboarding", "workspace").
- Botões no infinitivo ("Emitir apólice", "Revisar cotação"). Mensagens de erro dizem o que fazer
  ("Informe um LMI entre R$ 50.000,00 e R$ 500.000,00"), nunca só "Valor inválido".
- O dado de demonstração é rotulado: chip "Demonstração" na topbar e nota no login ("Nenhum dado pessoal
  real é armazenado; os registros são recriados diariamente"). CPF sempre mascarado (`***.482.917-**`).

---

## 9. Enxertos dos conceitos perdedores

### Do C (operação e densidade)
1. **Parcelas em tabela** (Parcela · Vencimento · Valor · Situação) no lugar da grade de 10 cards (§5.3).
2. **IDs e códigos em Geist Mono** (`AUTO-…`, `SIN-…`, `COT-…`, `RCF_DM`) em tabelas, feeds e linha do tempo.
   A coluna **Franquia** fica como coluna opcional do componente de tabela, ligada só se o domínio passar a
   ter franquia (§10.2).
3. **Filas salvas** na sidebar, com contadores por persona (§4.2).
4. **Atividade recente em tabela**, com coluna Responsável (avatar) e filtro de tipo (§5.3).
5. **Abas com contadores** no detalhe da apólice, incluindo Auditoria (§4.13).
6. **Gráfico** com projeção do mês tracejada, legenda explícita e tooltip "parcial até dd/mm" (§4.15).
7. **Selo de registro** com protocolo, rotulado "simulado" (§4.13).
8. **Cotação:** parcelamento segmentado, regra de negócio explicada no campo e faixa válida do LMI (§4.8,
   §4.10). O segmentado de "100/105/110% FIPE" não entra, porque no SPEC o LMI do Casco é sempre o valor do veículo.
9. **Modo compacto** de densidade (§3.13).
10. **Prefixo dentro do input** ("LMI R$").

### Do B (microinterações)
1. **Fatores como chips** com seta e cor de direção, mais o efeito em R$, sem perder a frase explicativa do A (§4.12).
2. **Cartões de escolha** (o padrão da franquia em 3 cards) para escolhas com preço: produto, tipo de imóvel (§4.10).
3. **Badge contextual** "1 sinistro em análise" na face da apólice (§4.6).
4. **"Ver como tabela"** no gráfico (§4.15).
5. **Abas de filtro + tempo relativo** na atividade (§5.3).
6. **Marcador "Hoje"** em linhas de tempo e cronogramas (§4.14).
7. **"1ª parcela absorve o ajuste de centavos"**, que é exatamente a regra do SPEC §4.4.
8. **Badge "Obrigatória"** com cadeado na cobertura obrigatória (§4.9).

### O que não levar
- Do B: zero cortado em valores monetários, barras verde-menta de baixo contraste (cerca de 1,6:1),
  cronograma em stepper horizontal, login em gradiente esmeralda e cards só com sombra.
- Do C: metadados de 12 px em cinza como padrão, faixa de KPIs sem número-herói e cabeçalho de apólice
  sem âncora visual.

---

## 10. Correções sobre a referência

### 10.1 Correções de design (o PNG mostra a versão antiga)
| Na referência | No produto |
|---|---|
| `R$` sobrescrito e centavos reduzidos em KPIs e listas | Só nos até 2 heróis por tela; KPIs com `R$` na linha de base; tabelas com valor completo |
| "Cancelar" como link coral sobre marinho | Botão perigo em contorno + modal com simulação e foco gerenciado |
| Tooltip estático cobrindo as barras de jul–set | Tooltip só em hover/foco |
| Parcelas em 10 cards | Tabela |
| Atividade como lista no desktop | Tabela com Responsável e filtro (lista mantida no mobile) |
| Códigos de cobertura em sans | `mono-id` |
| Marcador de meta `#B08D57` (2,6:1) | `--gold-text` (5,2:1) |
| Mobile sem renovações | Card de renovações após a distribuição |
| Tema escuro inexistente | Tokens da §3 |
| Fontes via Google Fonts e Tailwind por CDN | `@fontsource-variable/*` e Tailwind 4 compilado |

### 10.2 Aderência ao SPEC (a referência tem dados ilustrativos que o domínio não modela)
- **Franquia**, "100% FIPE" e carro reserva de 7/15/30 dias não existem no SPEC §4.2: o Casco tem LMI = valor
  do veículo; Carro reserva e Assistência são prêmio fixo. A UI segue o catálogo.
- **Fatores:** a referência aplica "fator combinado × subtotal". No SPEC, cada fator multiplica só os itens
  com taxa, e o prêmio líquido é a soma dos itens. O painel de cálculo segue o SPEC (§4.12).
- **Pagamento de parcelas** (paga em, débito em conta, x/10 pagas) não é modelado (`CronogramaParcelas` =
  número, vencimento, valor). A situação vem da data. Colunas Forma / Pago em e a barra de progresso só
  entram se o backend passar a expor pagamento.
- **Documentos de sinistro** ("4 sem documentos") e "Condições gerais SUSEP" não existem. Use o que o SPEC
  tem: triagem (score + alertas), status e histórico.
- **Corretora:** a referência mostra um seletor de corretora, mas o domínio não tem essa entidade. A sidebar
  mostra um bloco estático rotulado como corretora fictícia de demonstração (§4.2).
- **Volume:** o seed cria cerca de 10 apólices em 12 meses. Os componentes precisam funcionar com números
  pequenos e meses vazios; a referência usa 1.284 só para ilustrar o layout cheio.

---

## 11. Arquivos de referência

`docs/design/referencia/`, cópia byte a byte (SHA-256 conferido) do conceito vencedor:

| Arquivo | Conteúdo |
|---|---|
| `index.html` | As 4 telas desktop + dashboard mobile num só HTML estático (depende de CDN do Tailwind Play e de Google Fonts; serve só como referência visual, não como código de produção) |
| `login.png` | 1440 × 901: login com personas |
| `dashboard.png` | 1440 × 1307: visão da carteira |
| `apolice.png` | 1440 × 1665: detalhe da apólice (face/certificado) |
| `cotacao.png` | 1440 × 1306: assistente de cotação, etapa Coberturas, com memória de cálculo |
| `dashboard-mobile.png` | 390 × 1615 @2x: dashboard mobile com bottom nav |
