namespace Apolice.Domain.Cotacoes;

// "Expirada" é derivado do relógio e nunca persistido: evita um job só para virar status.
// Enum em vez da string do SPEC 4.3: a API serializa enums como texto, então o contrato continua "Expirada".
public enum StatusEfetivoCotacao
{
    Calculada,
    Aceita,
    Emitida,
    Recusada,
    Expirada,
}
