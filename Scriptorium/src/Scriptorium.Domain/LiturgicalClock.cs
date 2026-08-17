namespace Scriptorium.Domain;

/// <summary>
/// Fonte única da verdade para "que dia é hoje?" dentro da Abbatia.
///
/// PORQUÊ ISSO EXISTE (e não um simples <c>DateOnly.FromDateTime(DateTime.UtcNow)</c>
/// espalhado pelo código): containers Docker rodam em UTC por padrão, e o
/// Brasil está 3 horas ATRÁS do UTC (UTC-3, sem horário de verão desde
/// 2019). Isso significa que, todo dia, a partir das 21h em Brasília, o
/// relógio UTC do container JÁ VIROU para o dia seguinte — a API passaria a
/// devolver o devocional de "amanhã" três horas antes da meia-noite real do
/// usuário. Foi exatamente esse bug que o usuário reportou em produção: às
/// 22h40 de domingo (hora de Brasília), o app já mostrava a liturgia de
/// segunda-feira.
///
/// A correção não é trocar `DateTime.UtcNow` por `DateTime.Now` (que
/// dependeria do fuso configurado no SISTEMA OPERACIONAL do container — um
/// detalhe de infraestrutura fácil de ficar errado/inconsistente entre API e
/// Worker, ou entre ambientes). Em vez disso, convertemos EXPLICITAMENTE de
/// UTC para o fuso "America/Sao_Paulo" (identificador IANA, reconhecido em
/// qualquer imagem Docker baseada em Debian/Linux com tzdata instalado —
/// como as usadas pela API e pelo Worker, `mcr.microsoft.com/dotnet/*`) toda
/// vez que precisamos saber "que dia é hoje" do ponto de vista do usuário
/// final, mantendo o resto do sistema (timestamps de auditoria como
/// <c>UpdatedAtUtc</c>, agendamento do Worker) em UTC, que é a prática
/// correta para tudo que NÃO é "o dia do calendário litúrgico do usuário".
/// </summary>
public static class LiturgicalClock
{
    /// <summary>
    /// Fuso horário de referência da Abbatia — centralizado aqui em vez de
    /// repetir a string "America/Sao_Paulo" em cada lugar que precisa dela
    /// (a mesma classe de bug do LM Studio duplicado no docker-compose.yml:
    /// dois valores que deveriam ser sempre iguais, mas nada garante isso se
    /// forem cópias independentes).
    /// </summary>
    public static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>
    /// O "dia de hoje" na hora de Brasília, calculado a partir do instante
    /// UTC atual do relógio do sistema. Use este método em qualquer lugar
    /// que precise decidir "qual devocional é o de hoje" — nunca
    /// <c>DateOnly.FromDateTime(DateTime.UtcNow)</c> diretamente.
    /// </summary>
    public static DateOnly Today() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone));
}
