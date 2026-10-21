namespace Apolice.Domain.Apolices;

// Programada/Vencida são derivados da data: persistir só Ativa|Cancelada evita jobs de virada de status.
public enum StatusEfetivoApolice
{
    Ativa,
    Programada,
    Vencida,
    Cancelada,
}
