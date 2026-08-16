namespace Scriptorium.Worker.Options;

/// <summary>
/// Configuração de horário do BackgroundService, seguindo o mesmo Options
/// Pattern usado em <c>LmStudioOptions</c> (Scriptorium.Infrastructure) —
/// consistência de estilo ajuda muito na hora de aprender um projeto novo.
/// </summary>
public class WorkerScheduleOptions
{
    public const string SectionName = "WorkerSchedule";

    /// <summary>
    /// Hora do dia (em UTC, 0-23) em que o Worker deve rodar a raspagem
    /// diária. Padrão: 6 (que corresponde a ~03h da madrugada no horário de
    /// Brasília, UTC-3), atendendo ao requisito de "rodar na madrugada" sem
    /// competir por CPU/rede com o horário de pico de uso do app.
    /// </summary>
    public int HourUtc { get; set; } = 6;

    /// <summary>
    /// Quantos dias à frente (incluindo hoje) o Worker deve buscar e manter
    /// atualizados no banco a cada execução.
    /// </summary>
    public int DaysAhead { get; set; } = 7;

    /// <summary>
    /// Quando <c>true</c>, o Worker executa uma rodada imediatamente ao
    /// iniciar (além de continuar agendando as próximas execuções
    /// diárias). Muito útil em desenvolvimento/homelab: sem isso, você
    /// teria que esperar até a próxima madrugada para ver o Worker rodar
    /// pela primeira vez depois de subir o container.
    /// </summary>
    public bool RunImmediatelyOnStartup { get; set; } = true;
}
