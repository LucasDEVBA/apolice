/**
 * Contratos da API v1, copiados 1:1 dos records C# de src/Apolice.Application/Features/** (e dos
 * enums e de DadosRisco do domínio). Serão substituídos/alinhados aos tipos gerados de
 * docs/openapi/v1.json (pnpm gen:api → schema.d.ts) quando a API publicar o documento completo:
 * por isso os NOMES são os mesmos dos records, e a troca vira um re-export.
 *
 * Convenções do JSON (JsonOpcoes): camelCase; enum como string com o nome C# (PascalCase);
 * DateOnly "yyyy-MM-dd"; DateTimeOffset ISO 8601; decimal → number; nullable → `| null`
 * (DefaultIgnoreCondition = Never: o campo vem presente, com null).
 */

/** DateOnly serializado ("2026-10-21"). */
export type DateOnly = string;
/** DateTimeOffset serializado em ISO 8601. */
export type DateTimeOffset = string;
/** Guid serializado. */
export type Guid = string;

/* ---------------------------------------------------------------- enums do domínio */

export type Papel = "Corretor" | "Analista" | "Admin";
export type CodigoProduto = "AUTO" | "RESID" | "VIDA";
export type TipoCalculo = "TaxaSobreLmi" | "TaxaPorMil" | "PremioFixo";
export type TipoImovel = "Casa" | "Apartamento";
export type StatusEfetivoCotacao = "Calculada" | "Aceita" | "Emitida" | "Recusada" | "Expirada";
export type StatusEfetivoApolice = "Ativa" | "Programada" | "Vencida" | "Cancelada";
export type MotivoCancelamento = "PedidoDoSegurado" | "IniciativaSeguradora" | "Inadimplencia";
export type MetodoCancelamento = "Integral" | "PrazoCurto" | "ProRataDie";
export type TipoEndosso = "AlteracaoLmi" | "InclusaoCobertura" | "ExclusaoCobertura";
export type StatusSinistro = "Aberto" | "EmAnalise" | "Aprovado" | "Negado" | "Pago";
export type TipoNotificacao =
  "BoasVindas" | "Endosso" | "Cancelamento" | "SinistroAberto" | "SinistroDecidido";
export type EscopoDashboard = "Carteira" | "Global";

/* ---------------------------------------------------------------- comum */

/** Common/Pagina.cs — paginação por cursor (keyset). */
export interface Pagina<T> {
  itens: T[];
  proximoCursor: string | null;
}

/** Comum/SeguradoDto.cs — o detalhe traz o CPF completo formatado ("123.456.789-09"). */
export interface SeguradoDto {
  nome: string;
  cpf: string;
  email: string;
  dataNascimento: DateOnly;
}

/* ---------------------------------------------------------------- DadosRisco (polimórfico) */

/** Tarifacao/DadosRisco.cs — discriminador "produto". */
export interface DadosRiscoAuto {
  produto: "AUTO";
  valorVeiculo: number;
  anoFabricacao: number;
  cepPernoite: string;
  idadeCondutor: number;
  usoComercial: boolean;
  classeBonus: number;
}

export interface DadosRiscoResidencial {
  produto: "RESID";
  valorImovel: number;
  tipoImovel: TipoImovel;
  cep: string;
  habitual: boolean;
}

export interface DadosRiscoVida {
  produto: "VIDA";
  fumante: boolean;
}

export type DadosRisco = DadosRiscoAuto | DadosRiscoResidencial | DadosRiscoVida;

/* ---------------------------------------------------------------- auth */

/** Auth/AuthResponse.cs */
export interface UsuarioDto {
  id: Guid;
  nome: string;
  email: string;
  papel: Papel;
}

export interface AuthResponse {
  accessToken: string;
  expiraEm: DateTimeOffset;
  usuario: UsuarioDto;
}

/** Auth/Login/LoginRequest.cs */
export interface LoginRequest {
  email: string;
  senha: string;
}

/** Auth/LoginDemo/LoginDemoRequest.cs — persona: corretora | corretor | analista | admin. */
export interface LoginDemoRequest {
  persona: string;
}

/** Sistema/ObterFeatures/ObterFeaturesQuery.cs */
export interface FeaturesDto {
  loginDemo: boolean;
  triagemAutomaticaSinistros: boolean;
  documentoPdf: boolean;
  endossoOnline: boolean;
}

/* ---------------------------------------------------------------- produtos */

/** Produtos/ProdutoDto.cs */
export interface CoberturaProdutoDto {
  codigo: string;
  nome: string;
  descricao: string;
  obrigatoria: boolean;
  tipoCalculo: TipoCalculo;
  taxa: number;
  premioFixo: number | null;
  lmiMinimo: number;
  lmiMaximo: number;
  lmiFixo: number | null;
  permiteSinistro: boolean;
}

export interface ProdutoDto {
  codigo: CodigoProduto;
  nome: string;
  descricao: string;
  aliquotaIof: number;
  coberturas: CoberturaProdutoDto[];
}

/** Produtos/AtualizarTaxa/AtualizarTaxaRequest.cs — taxa em fração (0,032). */
export interface AtualizarTaxaRequest {
  taxa: number;
}

/* ---------------------------------------------------------------- cotações */

/** Cotacoes/CotacaoRequest.cs */
export interface SeguradoRequest {
  nome: string;
  cpf: string;
  email: string;
  dataNascimento: DateOnly;
}

export interface CoberturaRequest {
  codigo: string;
  lmi: number | null;
}

export interface CotacaoRequest {
  produtoCodigo: CodigoProduto;
  segurado: SeguradoRequest;
  dadosRisco: DadosRisco;
  coberturas: CoberturaRequest[];
}

/** Cotacoes/TarifacaoDto.cs */
export interface ItemTarifadoDto {
  codigo: string;
  nome: string;
  lmi: number;
  premioLiquido: number;
  obrigatoria: boolean;
  permiteSinistro: boolean;
}

export interface FatorDto {
  descricao: string;
  fator: number;
}

export interface TarifacaoDto {
  itens: ItemTarifadoDto[];
  fatores: FatorDto[];
  premioLiquido: number;
  aliquotaIof: number;
  iof: number;
  premioTotal: number;
  maxParcelas: number;
}

/** Cotacoes/CotacaoDto.cs */
export interface CotacaoDto {
  id: Guid;
  produtoCodigo: CodigoProduto;
  produtoNome: string;
  segurado: SeguradoDto;
  dadosRisco: DadosRisco;
  itens: ItemTarifadoDto[];
  fatores: FatorDto[];
  premioLiquido: number;
  aliquotaIof: number;
  iof: number;
  premioTotal: number;
  maxParcelas: number;
  status: StatusEfetivoCotacao;
  validaAte: DateTimeOffset;
  criadaEm: DateTimeOffset;
  corretorId: Guid;
  corretorNome: string;
  apoliceId: Guid | null;
}

export interface CotacaoResumoDto {
  id: Guid;
  produtoCodigo: CodigoProduto;
  seguradoNome: string;
  premioTotal: number;
  status: StatusEfetivoCotacao;
  validaAte: DateTimeOffset;
  criadaEm: DateTimeOffset;
  corretorNome: string;
}

/** Cotacoes/Listar/ListarCotacoesQuery.cs */
export interface ListarCotacoesQuery {
  status?: StatusEfetivoCotacao | null;
  produto?: CodigoProduto | null;
  busca?: string | null;
  cursor?: string | null;
  limite?: number | null;
}

/* ---------------------------------------------------------------- apólices */

/** Apolices/ApoliceDto.cs */
export interface CoberturaApoliceDto {
  codigo: string;
  nome: string;
  lmi: number;
  premioLiquido: number;
  obrigatoria: boolean;
  permiteSinistro: boolean;
}

export interface ParcelaDto {
  numero: number;
  vencimento: DateOnly;
  valor: number;
}

export interface EndossoDto {
  id: Guid;
  numero: number;
  tipo: TipoEndosso;
  coberturaCodigo: string;
  coberturaNome: string;
  lmiAnterior: number | null;
  lmiNovo: number | null;
  dataEfeito: DateOnly;
  ajusteLiquido: number;
  iofAjuste: number;
  ajusteTotal: number;
  criadoEm: DateTimeOffset;
  criadoPorNome: string;
}

export interface ApoliceDto {
  id: Guid;
  numero: string;
  cotacaoId: Guid;
  produtoCodigo: CodigoProduto;
  produtoNome: string;
  segurado: SeguradoDto;
  dadosRisco: DadosRisco;
  vigenciaInicio: DateOnly;
  vigenciaFim: DateOnly;
  coberturas: CoberturaApoliceDto[];
  premioLiquido: number;
  ajustesEndosso: number;
  premioLiquidoVigente: number;
  aliquotaIof: number;
  iof: number;
  premioTotal: number;
  parcelas: number;
  cronogramaParcelas: ParcelaDto[];
  status: StatusEfetivoApolice;
  emitidaEm: DateTimeOffset;
  canceladaEm: DateTimeOffset | null;
  motivoCancelamento: MotivoCancelamento | null;
  valorRestituicao: number | null;
  boasVindasEnviadasEm: DateTimeOffset | null;
  corretorId: Guid;
  corretorNome: string;
  endossos: EndossoDto[];
  sinistros: SinistroResumoDto[];
  notificacoes: NotificacaoDto[];
}

export interface ApoliceResumoDto {
  id: Guid;
  numero: string;
  produtoCodigo: CodigoProduto;
  seguradoNome: string;
  seguradoCpfMascarado: string;
  premioTotal: number;
  vigenciaInicio: DateOnly;
  vigenciaFim: DateOnly;
  status: StatusEfetivoApolice;
  corretorNome: string;
  emitidaEm: DateTimeOffset;
}

/** Apolices/Listar/ListarApolicesQuery.cs */
export interface ListarApolicesQuery {
  status?: StatusEfetivoApolice | null;
  produto?: CodigoProduto | null;
  busca?: string | null;
  cursor?: string | null;
  limite?: number | null;
}

/** Apolices/Emitir/EmitirApoliceRequest.cs */
export interface EmitirApoliceRequest {
  cotacaoId: Guid;
  inicioVigencia: DateOnly;
  parcelas: number;
}

/** Apolices/EndossoRequest.cs */
export interface EndossoRequest {
  tipo: TipoEndosso;
  coberturaCodigo: string;
  lmi: number | null;
}

/** Apolices/SimularEndosso/SimulacaoEndossoDto.cs */
export interface SimulacaoEndossoDto {
  dataEfeito: DateOnly;
  diasRestantes: number;
  diasVigencia: number;
  premioAnualAnterior: number;
  premioAnualNovo: number;
  ajusteLiquido: number;
  iofAjuste: number;
  ajusteTotal: number;
}

/** Apolices/CancelamentoRequest.cs */
export interface CancelamentoRequest {
  motivo: MotivoCancelamento;
}

/** Apolices/SimularCancelamento/SimulacaoCancelamentoDto.cs — percentualRetido é fração (0,13). */
export interface SimulacaoCancelamentoDto {
  dataCancelamento: DateOnly;
  diasDecorridos: number;
  diasVigencia: number;
  metodo: MetodoCancelamento;
  percentualRetido: number | null;
  premioLiquidoBase: number;
  valorRestituicao: number;
}

/* ---------------------------------------------------------------- sinistros */

/** Sinistros/SinistroDto.cs */
export interface TriagemDto {
  score: number;
  alertas: string[];
  triadoEm: DateTimeOffset;
}

export interface HistoricoSinistroDto {
  status: StatusSinistro;
  observacao: string | null;
  usuarioNome: string | null;
  ocorridoEm: DateTimeOffset;
}

export interface SinistroDto {
  id: Guid;
  numero: string;
  apoliceId: Guid;
  apoliceNumero: string;
  produtoCodigo: CodigoProduto;
  seguradoNome: string;
  coberturaCodigo: string;
  coberturaNome: string;
  lmiCobertura: number;
  dataOcorrencia: DateOnly;
  descricao: string;
  valorReclamado: number;
  valorIndenizacao: number | null;
  status: StatusSinistro;
  motivoNegativa: string | null;
  triagem: TriagemDto | null;
  abertoEm: DateTimeOffset;
  corretorNome: string;
  historico: HistoricoSinistroDto[];
}

export interface SinistroResumoDto {
  id: Guid;
  numero: string;
  apoliceNumero: string;
  seguradoNome: string;
  coberturaNome: string;
  valorReclamado: number;
  status: StatusSinistro;
  triagemScore: number | null;
  abertoEm: DateTimeOffset;
}

/** Sinistros/Listar/ListarSinistrosQuery.cs */
export interface ListarSinistrosQuery {
  status?: StatusSinistro | null;
  busca?: string | null;
  cursor?: string | null;
  limite?: number | null;
}

/** Sinistros/Abrir/AbrirSinistroRequest.cs */
export interface AbrirSinistroRequest {
  apoliceId: Guid;
  coberturaCodigo: string;
  dataOcorrencia: DateOnly;
  descricao: string;
  valorReclamado: number;
}

/** Sinistros/Aprovar/AprovarSinistroRequest.cs */
export interface AprovarSinistroRequest {
  valorIndenizacao: number;
  observacao: string | null;
}

/** Sinistros/Negar/NegarSinistroRequest.cs */
export interface NegarSinistroRequest {
  motivo: string;
}

/* ---------------------------------------------------------------- notificações, dashboard, auditoria */

/** Notificacoes/NotificacaoDto.cs — e-mails simulados (caixa de saída). */
export interface NotificacaoDto {
  id: Guid;
  tipo: TipoNotificacao;
  destinatario: string;
  assunto: string;
  corpo: string;
  criadaEm: DateTimeOffset;
}

/** Dashboard/DashboardDto.cs — frações seguem o domínio (0,25 = 25%). */
export interface PremioMensalDto {
  /** "yyyy-MM" */
  mes: string;
  valor: number;
}

export interface ProdutoDashboardDto {
  produtoCodigo: CodigoProduto;
  produtoNome: string;
  apolicesAtivas: number;
  premioEmitido: number;
}

export interface DashboardDto {
  escopo: EscopoDashboard;
  apolicesAtivas: number;
  premioEmitidoMes: number;
  premioEmitido12Meses: PremioMensalDto[];
  cotacoesAbertas: number;
  taxaConversao90d: number;
  sinistrosEmAberto: number;
  sinistralidade12m: number;
  porProduto: ProdutoDashboardDto[];
}

/** Auditoria/AuditoriaDto.cs */
export interface AuditoriaDto {
  id: Guid;
  usuarioNome: string | null;
  acao: string;
  entidade: string;
  entidadeId: string | null;
  detalhes: string | null;
  correlationId: string | null;
  ocorridoEm: DateTimeOffset;
}

/** Auditoria/Listar/ListarAuditoriaQuery.cs */
export interface ListarAuditoriaQuery {
  cursor?: string | null;
  limite?: number | null;
}

/* ---------------------------------------------------------------- erro (RFC 9457) */

/**
 * ProblemDetails do SPEC §7: type "urn:apolice:problema:{codigo}" + extensões codigo, traceId e
 * correlationId; ValidationProblemDetails acrescenta errors por campo.
 */
export interface ProblemDetails {
  type?: string | null;
  title?: string | null;
  status?: number | null;
  detail?: string | null;
  instance?: string | null;
  codigo?: string;
  traceId?: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}
