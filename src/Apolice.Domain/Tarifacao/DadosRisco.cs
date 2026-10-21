using System.Text.Json.Serialization;
using Apolice.Domain.Produtos;

namespace Apolice.Domain.Tarifacao;

// O discriminador "produto" é o próprio código do produto: o mesmo JSON serve à API e à coluna text.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "produto")]
[JsonDerivedType(typeof(DadosRiscoAuto), CodigosProduto.Auto)]
[JsonDerivedType(typeof(DadosRiscoResidencial), CodigosProduto.Residencial)]
[JsonDerivedType(typeof(DadosRiscoVida), CodigosProduto.Vida)]
public abstract record DadosRisco
{
    [JsonIgnore]
    public abstract string ProdutoCodigo { get; }
}

public sealed record DadosRiscoAuto(
    decimal ValorVeiculo,
    int AnoFabricacao,
    string CepPernoite,
    int IdadeCondutor,
    bool UsoComercial,
    int ClasseBonus) : DadosRisco
{
    [JsonIgnore]
    public override string ProdutoCodigo => CodigosProduto.Auto;
}

public sealed record DadosRiscoResidencial(
    decimal ValorImovel,
    TipoImovel TipoImovel,
    string Cep,
    bool Habitual) : DadosRisco
{
    [JsonIgnore]
    public override string ProdutoCodigo => CodigosProduto.Residencial;
}

// A idade vem de Segurado.DataNascimento para não existir duas fontes da mesma informação.
public sealed record DadosRiscoVida(bool Fumante) : DadosRisco
{
    [JsonIgnore]
    public override string ProdutoCodigo => CodigosProduto.Vida;
}
