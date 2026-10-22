using Apolice.Infrastructure.Outbox;
using Apolice.Infrastructure.Persistence.Registros;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apolice.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_mensagens");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Tipo).HasMaxLength(OutboxMessage.TamanhoMaximoTipo).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.Traceparent).HasMaxLength(OutboxMessage.TamanhoMaximoTraceparent);
        builder.Property(m => m.CorrelationId).HasMaxLength(Tamanhos.CorrelationId);
        builder.Property(m => m.UltimoErro).HasMaxLength(OutboxMessage.TamanhoMaximoErro);

        // Sem índice parcial (o DSQL não tem): o composto atende "processado_em IS NULL ORDER BY ocorrido_em".
        builder.HasIndex(m => new { m.ProcessadoEm, m.OcorridoEm });
    }
}

internal sealed class MensagemProcessadaConfiguration : IEntityTypeConfiguration<MensagemProcessada>
{
    public void Configure(EntityTypeBuilder<MensagemProcessada> builder)
    {
        builder.ToTable("mensagens_processadas");
        builder.HasKey(m => new { m.MensagemId, m.Consumidor });
        builder.Property(m => m.Consumidor).HasMaxLength(MensagemProcessada.TamanhoMaximoConsumidor);
    }
}

internal sealed class AuditoriaRegistroConfiguration : IEntityTypeConfiguration<AuditoriaRegistro>
{
    public void Configure(EntityTypeBuilder<AuditoriaRegistro> builder)
    {
        builder.ToTable("auditoria");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Acao).HasMaxLength(AuditoriaRegistro.TamanhoMaximoAcao).IsRequired();
        builder.Property(a => a.Entidade).HasMaxLength(AuditoriaRegistro.TamanhoMaximoEntidade).IsRequired();
        builder.Property(a => a.EntidadeId).HasMaxLength(AuditoriaRegistro.TamanhoMaximoEntidadeId);
        builder.Property(a => a.Detalhes).HasMaxLength(AuditoriaRegistro.TamanhoMaximoDetalhes);
        builder.Property(a => a.CorrelationId).HasMaxLength(Tamanhos.CorrelationId);

        // Sem FK para usuarios: a trilha sobrevive ao cadastro e registra também tentativas anônimas.
        builder.HasIndex(a => new { a.OcorridoEm, a.Id });
    }
}

internal sealed class IdempotenciaRegistroConfiguration : IEntityTypeConfiguration<IdempotenciaRegistro>
{
    public void Configure(EntityTypeBuilder<IdempotenciaRegistro> builder)
    {
        builder.ToTable("idempotencia");
        builder.HasKey(i => new { i.UsuarioId, i.Chave });
        builder.Property(i => i.Chave).HasMaxLength(IdempotenciaRegistro.TamanhoMaximoChave);
        builder.Property(i => i.HashRequisicao).HasMaxLength(IdempotenciaRegistro.TamanhoHash).IsRequired();
        builder.Property(i => i.ContentType).HasMaxLength(IdempotenciaRegistro.TamanhoMaximoContentType);
        builder.Property(i => i.Corpo).IsRequired();
        builder.HasIndex(i => i.ExpiraEm);
    }
}

internal sealed class SequenciaNumeradorConfiguration : IEntityTypeConfiguration<SequenciaNumerador>
{
    public void Configure(EntityTypeBuilder<SequenciaNumerador> builder)
    {
        builder.ToTable("numeradores");
        builder.HasKey(n => n.Chave);
        builder.Property(n => n.Chave).HasMaxLength(SequenciaNumerador.TamanhoMaximoChave);
    }
}
