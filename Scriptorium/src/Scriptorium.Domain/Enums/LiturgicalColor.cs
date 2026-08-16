namespace Scriptorium.Domain.Enums;

/// <summary>
/// As cores litúrgicas usadas pela Igreja Católica para paramentos e ornamentos,
/// cada uma carregando um simbolismo espiritual específico. Modelamos isso como
/// enum (em vez de apenas guardar uma string "Branco", "Verde" etc.) porque:
///
/// 1) EVITA ERROS DE DIGITAÇÃO: o compilador nos impede de escrever "branco"
///    (minúsculo) ou "Blanco" (erro de digitação) sem perceber.
/// 2) FACILITA REGRAS DE NEGÓCIO: podemos futuramente ter um switch/expression
///    que decide, por exemplo, qual ícone ou cor de fundo mostrar no app
///    Oratorium baseado neste valor, com checagem em tempo de compilação.
/// 3) DOCUMENTA O DOMÍNIO: qualquer desenvolvedor que abrir este arquivo entende
///    imediatamente quais são os únicos valores válidos, sem precisar caçar
///    strings mágicas espalhadas pelo código.
///
/// A fonte de scraping (gcatholic.org) representa a cor com uma letra dentro
/// de um atributo CSS (ex: &lt;span class="feastw"&gt; = White/Branco,
/// "feastg" = green/Verde). O scraper concreto (GCatholicCalendarScraper)
/// é responsável por traduzir essa letra para um destes valores de enum.
/// </summary>
public enum LiturgicalColor
{
    /// <summary>Verde: Tempo Comum. Simboliza esperança e vida.</summary>
    Verde = 0,

    /// <summary>Branco (ou Ouro): festas do Senhor, de Nossa Senhora, dos
    /// santos não-mártires, tempo pascal e natalino. Simboliza pureza e glória.</summary>
    Branco = 1,

    /// <summary>Vermelho: Paixão, Pentecostes, festas de mártires e apóstolos.
    /// Simboliza o sangue derramado e o fogo do Espírito Santo.</summary>
    Vermelho = 2,

    /// <summary>Roxo (Violeta): Advento e Quaresma. Simboliza penitência e
    /// preparação espiritual.</summary>
    Roxo = 3,

    /// <summary>Rosa: usado apenas duas vezes ao ano (3º domingo do Advento —
    /// "Gaudete" — e 4º domingo da Quaresma — "Laetare"). Simboliza alegria
    /// no meio da penitência.</summary>
    Rosa = 4,

    /// <summary>Preto: usado em Missas de Finados/exéquias em algumas tradições.
    /// Raro no calendário atual, mas mantido para completude.</summary>
    Preto = 5,
}
