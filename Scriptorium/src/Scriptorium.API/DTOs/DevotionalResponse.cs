using Scriptorium.Domain.Entities;
using Scriptorium.Domain.Enums;

namespace Scriptorium.API.DTOs;

/// <summary>
/// DTOs (Data Transfer Objects) de RESPOSTA da API — o formato exato de JSON
/// que o app Oratorium (o frontend, ainda por vir) vai receber.
///
/// PORQUÊ NÃO DEVOLVER AS ENTIDADES DE DOMAIN DIRETO NO JSON DA API (ex:
/// simplesmente fazer `Results.Ok(devotional)` com a entidade
/// <see cref="Domain.Entities.DailyDevotional"/>)?
///
/// 1) ACOPLAMENTO: se devolvêssemos a entidade de Domain direto, qualquer
///    mudança na estrutura do banco (ex: renomear uma coluna, adicionar uma
///    propriedade de navegação nova) QUEBRARIA o contrato público da API
///    sem querermos — o frontend ficaria refém de detalhes internos do
///    banco de dados. Com um DTO dedicado, controlamos EXPLICITAMENTE o que
///    é exposto, podendo evoluir o banco livremente por baixo.
/// 2) REFERÊNCIAS CIRCULARES: nossas entidades têm propriedades de
///    navegação em ambas as direções (Reading.DailyDevotional aponta de
///    volta para o pai) — serializar isso direto para JSON causaria um erro
///    de referência circular (ou exigiria configuração especial do
///    serializador, o que é mais frágil do que simplesmente moldar a forma
///    exata do JSON que queremos).
/// 3) CLAREZA PARA O CONSUMIDOR: o DTO expõe só os campos relevantes para a
///    tela de devocional, já formatados do jeito mais conveniente para o
///    cliente (ex: o enum LiturgicalColor vira uma string legível
///    "Branco" em vez do número 1).
/// </summary>
public sealed record DevotionalResponse(
    string Date,
    string LiturgicalTitle,
    string LiturgicalColor,
    SaintResponse? Saint,
    IReadOnlyList<ReadingResponse> Readings,
    HomilyResponse? Homily,
    IReadOnlyList<OtherSaintResponse> OtherSaints)
{
    /// <summary>
    /// Método de "mapeamento" (converte a entidade de Domain para o DTO de
    /// resposta). Colocamos essa lógica como um método estático de fábrica
    /// bem próximo do próprio DTO — uma alternativa mais simples do que
    /// trazer uma biblioteca de mapeamento (como AutoMapper) para um projeto
    /// deste tamanho, onde "na mão" é mais transparente e fácil de debugar
    /// (você vê exatamente onde cada campo do JSON vem, sem mágica de
    /// reflection escondida).
    /// </summary>
    public static DevotionalResponse FromEntity(DailyDevotional entity) => new(
        Date: entity.Date.ToString("yyyy-MM-dd"),
        LiturgicalTitle: entity.LiturgicalTitle,
        LiturgicalColor: entity.Color.ToString(),
        Saint: entity.Saint is null ? null : SaintResponse.FromEntity(entity.Saint),
        Readings: entity.Readings
            .OrderBy(r => r.Type) // Garante a ordem litúrgica correta (1ª Leitura, Salmo, 2ª Leitura, Evangelho) independente da ordem em que vieram do banco.
            .Select(ReadingResponse.FromEntity)
            .ToList(),
        Homily: entity.Homily is null ? null : HomilyResponse.FromEntity(entity.Homily),
        OtherSaints: entity.OtherSaints.Select(OtherSaintResponse.FromEntity).ToList());
}

public sealed record SaintResponse(string Name, string Biography, string? ImageUrl)
{
    public static SaintResponse FromEntity(SaintOfTheDay entity) =>
        new(entity.Name, entity.Biography, entity.ImageUrl);
}

public sealed record OtherSaintResponse(string Name, string ShortBiography)
{
    public static OtherSaintResponse FromEntity(OtherSaintOfDay entity) =>
        new(entity.Name, entity.ShortBiography);
}

public sealed record ReadingResponse(string Type, string Reference, string Text)
{
    public static ReadingResponse FromEntity(Reading entity) =>
        new(entity.Type.ToString(), entity.Reference, entity.Text);
}

/// <summary>
/// Resposta da homilia já resolve, no backend, "qual texto mostrar" — o
/// frontend não precisa saber nada sobre o processo de tradução; ele só
/// recebe <see cref="DisplayText"/> pronto (o traduzido, se disponível, ou o
/// original como fallback) e um indicador simples
/// <see cref="IsAwaitingTranslation"/> para, opcionalmente, mostrar um aviso
/// discreto tipo "tradução em processamento" na UI.
/// </summary>
public sealed record HomilyResponse(
    string Title,
    string DisplayText,
    bool IsAwaitingTranslation,
    string SourceUrl)
{
    public static HomilyResponse FromEntity(Homily entity)
    {
        var displayText = entity.Status == TranslationStatus.Concluida || entity.Status == TranslationStatus.NaoRequerida
            ? entity.TextoTraduzido ?? entity.TextoOriginal
            : entity.TextoOriginal;

        var isAwaitingTranslation = entity.Status is TranslationStatus.Pendente or TranslationStatus.FalhouTentativa;

        return new HomilyResponse(entity.Title, displayText, isAwaitingTranslation, entity.SourceUrl);
    }
}
