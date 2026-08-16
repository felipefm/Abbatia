namespace Scriptorium.Domain.Enums;

/// <summary>
/// Identifica qual "papel" uma leitura bíblica desempenha dentro da liturgia
/// do dia. A missa tem uma estrutura fixa e sempre segue esta ordem — por
/// isso vale a pena modelar como enum ao invés de apenas confiar na ordem
/// de inserção numa lista (que seria uma "regra de negócio invisível",
/// fácil de quebrar sem querer num refactor futuro).
/// </summary>
public enum ReadingType
{
    /// <summary>Primeira leitura (geralmente Antigo Testamento, exceto no
    /// Tempo Pascal, quando é do livro de Atos dos Apóstolos).</summary>
    PrimeiraLeitura = 0,

    /// <summary>Salmo responsorial, cantado/recitado entre as leituras.</summary>
    SalmoResponsorial = 1,

    /// <summary>Segunda leitura (Novo Testamento, epístolas) — presente
    /// apenas aos domingos e solenidades, não em dias de semana comuns.</summary>
    SegundaLeitura = 2,

    /// <summary>O Evangelho, ápice da liturgia da Palavra.</summary>
    Evangelho = 3,
}
