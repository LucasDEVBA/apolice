using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apolice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auditoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    acao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidade = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidade_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    detalhes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auditoria", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "idempotencia",
                columns: table => new
                {
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    hash_requisicao = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    corpo = table.Column<string>(type: "text", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotencia", x => new { x.usuario_id, x.chave });
                });

            migrationBuilder.CreateTable(
                name: "mensagens_processadas",
                columns: table => new
                {
                    mensagem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumidor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    processada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mensagens_processadas", x => new { x.mensagem_id, x.consumidor });
                });

            migrationBuilder.CreateTable(
                name: "notificacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    referencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    destinatario = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    assunto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    corpo = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notificacoes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "numeradores",
                columns: table => new
                {
                    chave = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ultimo = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_numeradores", x => x.chave);
                });

            migrationBuilder.CreateTable(
                name: "outbox_mensagens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    traceparent = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    processado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    ultimo_erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_mensagens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "produtos",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    aliquota_iof = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produtos", x => x.codigo);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    senha_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    papel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    falhas_login = table.Column<int>(type: "integer", nullable: false),
                    bloqueado_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "coberturas_produto",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    produto_codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    obrigatoria = table.Column<bool>(type: "boolean", nullable: false),
                    tipo_calculo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    taxa = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    premio_fixo = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    lmi_minimo = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    lmi_maximo = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    lmi_fixo = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    permite_sinistro = table.Column<bool>(type: "boolean", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coberturas_produto", x => new { x.produto_codigo, x.codigo });
                    table.ForeignKey(
                        name: "fk_coberturas_produto_produtos_produto_codigo",
                        column: x => x.produto_codigo,
                        principalTable: "produtos",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cotacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    corretor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produto_codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    dados_risco = table.Column<string>(type: "text", nullable: false),
                    premio_liquido = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    aliquota_iof = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    iof = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    premio_total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    max_parcelas = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    valida_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    apolice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    segurado_cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    segurado_data_nascimento = table.Column<DateOnly>(type: "date", nullable: false),
                    segurado_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    segurado_nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cotacoes", x => x.id);
                    table.ForeignKey(
                        name: "fk_cotacoes_produtos_produto_codigo",
                        column: x => x.produto_codigo,
                        principalTable: "produtos",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cotacoes_usuarios_corretor_id",
                        column: x => x.corretor_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    familia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revogado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    substituido_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "apolices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cotacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    corretor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produto_codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    dados_risco = table.Column<string>(type: "text", nullable: false),
                    premio_liquido = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    iof = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    premio_total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    aliquota_iof = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    parcelas = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    emitida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancelada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_cancelamento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    valor_restituicao = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    boas_vindas_enviadas_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    segurado_cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    segurado_data_nascimento = table.Column<DateOnly>(type: "date", nullable: false),
                    segurado_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    segurado_nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_apolices", x => x.id);
                    table.ForeignKey(
                        name: "fk_apolices_cotacoes_cotacao_id",
                        column: x => x.cotacao_id,
                        principalTable: "cotacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_apolices_produtos_produto_codigo",
                        column: x => x.produto_codigo,
                        principalTable: "produtos",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_apolices_usuarios_corretor_id",
                        column: x => x.corretor_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cotacao_fatores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fator = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    cotacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cotacao_fatores", x => x.id);
                    table.ForeignKey(
                        name: "fk_cotacao_fatores_cotacoes_cotacao_id",
                        column: x => x.cotacao_id,
                        principalTable: "cotacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cotacao_itens",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cotacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    lmi = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    premio_liquido = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    obrigatoria = table.Column<bool>(type: "boolean", nullable: false),
                    permite_sinistro = table.Column<bool>(type: "boolean", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cotacao_itens", x => new { x.cotacao_id, x.codigo });
                    table.ForeignKey(
                        name: "fk_cotacao_itens_cotacoes_cotacao_id",
                        column: x => x.cotacao_id,
                        principalTable: "cotacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "apolice_coberturas",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    apolice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    lmi = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    premio_liquido = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    obrigatoria = table.Column<bool>(type: "boolean", nullable: false),
                    permite_sinistro = table.Column<bool>(type: "boolean", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_apolice_coberturas", x => new { x.apolice_id, x.codigo });
                    table.ForeignKey(
                        name: "fk_apolice_coberturas_apolices_apolice_id",
                        column: x => x.apolice_id,
                        principalTable: "apolices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "endossos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    apolice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cobertura_codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cobertura_nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    cobertura_permite_sinistro = table.Column<bool>(type: "boolean", nullable: false),
                    lmi_anterior = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    lmi_novo = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    data_efeito = table.Column<DateOnly>(type: "date", nullable: false),
                    premio_anual_anterior = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    premio_anual_novo = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ajuste_liquido = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    iof_ajuste = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ajuste_total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    criado_por_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_endossos", x => x.id);
                    table.ForeignKey(
                        name: "fk_endossos_apolices_apolice_id",
                        column: x => x.apolice_id,
                        principalTable: "apolices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_endossos_usuarios_criado_por_id",
                        column: x => x.criado_por_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sinistros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    apolice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    apolice_numero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    corretor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produto_codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    segurado_nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    segurado_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    cobertura_codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cobertura_nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    lmi_cobertura = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    data_ocorrencia = table.Column<DateOnly>(type: "date", nullable: false),
                    descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    valor_reclamado = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    valor_indenizacao = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    motivo_negativa = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    aberto_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    triagem_alertas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    triagem_score = table.Column<int>(type: "integer", nullable: true),
                    triagem_triado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sinistros", x => x.id);
                    table.ForeignKey(
                        name: "fk_sinistros_apolices_apolice_id",
                        column: x => x.apolice_id,
                        principalTable: "apolices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sinistros_usuarios_corretor_id",
                        column: x => x.corretor_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sinistro_historico",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    sinistro_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sinistro_historico", x => x.id);
                    table.ForeignKey(
                        name: "fk_sinistro_historico_sinistros_sinistro_id",
                        column: x => x.sinistro_id,
                        principalTable: "sinistros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sinistro_historico_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_apolices_corretor_id_emitida_em",
                table: "apolices",
                columns: new[] { "corretor_id", "emitida_em" });

            migrationBuilder.CreateIndex(
                name: "ix_apolices_cotacao_id",
                table: "apolices",
                column: "cotacao_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_apolices_emitida_em_id",
                table: "apolices",
                columns: new[] { "emitida_em", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_apolices_numero",
                table: "apolices",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_apolices_produto_codigo",
                table: "apolices",
                column: "produto_codigo");

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_ocorrido_em_id",
                table: "auditoria",
                columns: new[] { "ocorrido_em", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_cotacao_fatores_cotacao_id",
                table: "cotacao_fatores",
                column: "cotacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_cotacoes_corretor_id_criada_em",
                table: "cotacoes",
                columns: new[] { "corretor_id", "criada_em" });

            migrationBuilder.CreateIndex(
                name: "ix_cotacoes_criada_em_id",
                table: "cotacoes",
                columns: new[] { "criada_em", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_cotacoes_produto_codigo",
                table: "cotacoes",
                column: "produto_codigo");

            migrationBuilder.CreateIndex(
                name: "ix_endossos_apolice_id_numero",
                table: "endossos",
                columns: new[] { "apolice_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_endossos_criado_por_id",
                table: "endossos",
                column: "criado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_idempotencia_expira_em",
                table: "idempotencia",
                column: "expira_em");

            migrationBuilder.CreateIndex(
                name: "ix_notificacoes_referencia_id",
                table: "notificacoes",
                column: "referencia_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_mensagens_processado_em_ocorrido_em",
                table: "outbox_mensagens",
                columns: new[] { "processado_em", "ocorrido_em" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_familia_id",
                table: "refresh_tokens",
                column: "familia_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_usuario_id",
                table: "refresh_tokens",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_sinistro_historico_sinistro_id",
                table: "sinistro_historico",
                column: "sinistro_id");

            migrationBuilder.CreateIndex(
                name: "ix_sinistro_historico_usuario_id",
                table: "sinistro_historico",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_sinistros_aberto_em_id",
                table: "sinistros",
                columns: new[] { "aberto_em", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_sinistros_apolice_id",
                table: "sinistros",
                column: "apolice_id");

            migrationBuilder.CreateIndex(
                name: "ix_sinistros_corretor_id_aberto_em",
                table: "sinistros",
                columns: new[] { "corretor_id", "aberto_em" });

            migrationBuilder.CreateIndex(
                name: "ix_sinistros_numero",
                table: "sinistros",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_email",
                table: "usuarios",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "apolice_coberturas");

            migrationBuilder.DropTable(
                name: "auditoria");

            migrationBuilder.DropTable(
                name: "coberturas_produto");

            migrationBuilder.DropTable(
                name: "cotacao_fatores");

            migrationBuilder.DropTable(
                name: "cotacao_itens");

            migrationBuilder.DropTable(
                name: "endossos");

            migrationBuilder.DropTable(
                name: "idempotencia");

            migrationBuilder.DropTable(
                name: "mensagens_processadas");

            migrationBuilder.DropTable(
                name: "notificacoes");

            migrationBuilder.DropTable(
                name: "numeradores");

            migrationBuilder.DropTable(
                name: "outbox_mensagens");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "sinistro_historico");

            migrationBuilder.DropTable(
                name: "sinistros");

            migrationBuilder.DropTable(
                name: "apolices");

            migrationBuilder.DropTable(
                name: "cotacoes");

            migrationBuilder.DropTable(
                name: "produtos");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
