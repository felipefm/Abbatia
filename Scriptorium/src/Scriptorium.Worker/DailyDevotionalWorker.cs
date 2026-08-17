using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scriptorium.Application.Interfaces;
using Scriptorium.Application.Services;
using Scriptorium.Domain;
using Scriptorium.Domain.Enums;
using Scriptorium.Infrastructure.Data;
using Scriptorium.Worker.Options;

namespace Scriptorium.Worker;

/// <summary>
/// O CORAÇÃO OPERACIONAL da Abbatia: um <see cref="BackgroundService"/> que
/// roda continuamente em segundo plano dentro do processo do Worker,
/// acordando uma vez por dia (de madrugada, por padrão) para:
///
/// 1) Raspar a liturgia dos próximos N dias (padrão: 7) usando os 4
///    scrapers (via <see cref="DevotionalBuilderService"/>, que já
///    encapsula a orquestração e o padrão Strategy).
/// 2) Tentar traduzir tudo que estiver pendente (incluindo homilias que já
///    estavam no banco de execuções anteriores e falharam por o LM Studio
///    estar desligado naquele momento).
/// 3) Persistir tudo no SQLite.
///
/// O QUE É UM BackgroundService (para quem está aprendendo)?
/// É uma classe-base do ASP.NET Core / Generic Host que roda uma tarefa de
/// longa duração dentro do MESMO PROCESSO da aplicação, mas fora do
/// caminho de requisições HTTP (diferente de um Controller/endpoint, que só
/// executa quando alguém faz uma chamada). O método
/// <see cref="ExecuteAsync"/> é chamado UMA VEZ quando o host inicia, e
/// deve conter o SEU PRÓPRIO laço de repetição (`while`) — o framework não
/// fica chamando ExecuteAsync de novo sozinho; é responsabilidade da nossa
/// classe ficar "viva" e se auto-agendar.
/// </summary>
public class DailyDevotionalWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerScheduleOptions> scheduleOptions,
    ILogger<DailyDevotionalWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ao subir o container, garantimos que o schema do banco está em
        // dia ANTES de qualquer tentativa de escrita — aplica automaticamente
        // qualquer migration pendente (equivalente a rodar
        // `dotnet ef database update` manualmente, mas em tempo de execução).
        // Isso é o que permite ao operador da homelab simplesmente rodar
        // `docker compose up` sem precisar entrar no container para migrar
        // o banco manualmente.
        await ApplyPendingMigrationsAsync(stoppingToken);

        var schedule = scheduleOptions.Value;

        if (schedule.RunImmediatelyOnStartup)
        {
            logger.LogInformation("RunImmediatelyOnStartup=true: executando uma rodada de raspagem imediatamente.");
            await RunSafelyAsync(stoppingToken);
        }

        // LAÇO PRINCIPAL: calcula quanto tempo falta até o próximo horário
        // agendado (ex: 06h UTC), espera esse tempo (sem bloquear a thread —
        // Task.Delay é assíncrono) e então executa uma nova rodada. Repete
        // "para sempre" até que o token de cancelamento seja disparado (ex:
        // quando o container recebe um sinal de shutdown do Docker).
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = CalculateDelayUntilNextRun(schedule.HourUtc);
            logger.LogInformation(
                "Próxima raspagem agendada para {ProximaExecucao:yyyy-MM-dd HH:mm} UTC (em {Delay}).",
                DateTime.UtcNow.Add(delay),
                delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Cancelamento solicitado durante a espera (shutdown do
                // container) — encerra o laço graciosamente, sem logar como erro.
                break;
            }

            await RunSafelyAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Calcula quanto tempo (TimeSpan) falta até a próxima ocorrência da
    /// hora UTC configurada. Se já passamos do horário hoje, agenda para
    /// amanhã no mesmo horário.
    /// </summary>
    internal static TimeSpan CalculateDelayUntilNextRun(int hourUtc, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var todayAtHour = new DateTime(now.Year, now.Month, now.Day, hourUtc, 0, 0, DateTimeKind.Utc);

        var nextRun = todayAtHour > now ? todayAtHour : todayAtHour.AddDays(1);
        return nextRun - now;
    }

    /// <summary>
    /// Envolve <see cref="RunOnceAsync"/> com tratamento de exceção "de
    /// última linha de defesa": mesmo que algo dê muito errado (bug não
    /// previsto, exceção não tratada em alguma camada mais interna), o
    /// BackgroundService NUNCA deve morrer — se ele morrer, o Worker inteiro
    /// para de rodar (o Generic Host encerra o processo quando um
    /// BackgroundService lança uma exceção não tratada de dentro de
    /// ExecuteAsync). Preferimos logar o erro e tentar de novo no próximo
    /// ciclo agendado a derrubar todo o serviço por causa de UMA falha
    /// pontual (ex: um site fora do ar justamente naquela madrugada).
    /// </summary>
    private async Task RunSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RunOnceAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha inesperada durante a rodada de raspagem. Será tentado novamente no próximo ciclo agendado.");
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        // Cada rodada cria seu PRÓPRIO escopo de injeção de dependência.
        // PORQUÊ ISSO É NECESSÁRIO: o DailyDevotionalWorker em si é
        // registrado como Singleton (todo IHostedService é), mas o
        // ScriptoriumDbContext do EF Core NÃO É thread-safe e não deveria
        // viver pela duração inteira da aplicação (ele mantém um cache de
        // rastreamento de entidades que cresceria sem limite). Criar um
        // novo escopo a cada rodada garante uma instância "fresca" do
        // DbContext (e de tudo mais registrado como Scoped) por execução —
        // o mesmo padrão usado implicitamente pelo ASP.NET Core a cada
        // requisição HTTP, replicado aqui manualmente porque não estamos
        // dentro de um pipeline de requisições.
        using var scope = scopeFactory.CreateScope();

        var builder = scope.ServiceProvider.GetRequiredService<DevotionalBuilderService>();
        var repository = scope.ServiceProvider.GetRequiredService<IDevotionalRepository>();
        var translationService = scope.ServiceProvider.GetRequiredService<ITranslationService>();
        var schedule = scheduleOptions.Value;

        logger.LogInformation("Iniciando rodada de raspagem para os próximos {Dias} dia(s).", schedule.DaysAhead);

        // Usa o fuso de Brasília (não UTC puro) para decidir qual é "o dia
        // de hoje" — ver Scriptorium.Domain.LiturgicalClock para o porquê
        // (o container roda em UTC, e Brasília está 3h atrás; sem isso, a
        // janela de "próximos N dias" processada aqui ficaria adiantada em
        // relação ao calendário real do usuário).
        var today = LiturgicalClock.Today();

        for (var i = 0; i < schedule.DaysAhead; i++)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var date = today.AddDays(i);

            try
            {
                logger.LogInformation("Processando devocional de {Data:yyyy-MM-dd}...", date);
                var devotional = await builder.BuildAsync(date, stoppingToken);
                await repository.UpsertAsync(devotional, stoppingToken);
                logger.LogInformation(
                    "Devocional de {Data:yyyy-MM-dd} salvo (Santo: {TemSanto}, Leituras: {QtdLeituras}, Homilia: {TemHomilia}).",
                    date,
                    devotional.Saint is not null,
                    devotional.Readings.Count,
                    devotional.Homily is not null);
            }
            catch (Exception ex)
            {
                // Isola a falha de UM DIA específico — se dia 3 dos 7 falhar
                // por algum motivo, ainda queremos que os dias 4, 5, 6 e 7
                // sejam processados normalmente.
                logger.LogError(ex, "Falha ao processar o devocional de {Data:yyyy-MM-dd}.", date);
            }
        }

        await RetryPendingTranslationsAsync(repository, translationService, stoppingToken);
    }

    /// <summary>
    /// Varre o banco em busca de homilias cujo texto ainda não foi
    /// traduzido com sucesso (status Pendente ou FalhouTentativa) e tenta
    /// traduzir de novo. Este é o mecanismo que cumpre, na prática, o
    /// requisito "se a IA local estiver desligada, tente novamente depois":
    /// como este método roda TODA vez que o Worker executa (inclusive em
    /// dias seguintes), uma homilia que falhou ontem porque o LM Studio
    /// estava desligado tem uma nova chance de ser traduzida hoje, sem
    /// precisar re-raspar nada do Vaticano de novo (o texto original já
    /// está salvo no banco).
    /// </summary>
    private async Task RetryPendingTranslationsAsync(
        IDevotionalRepository repository,
        ITranslationService translationService,
        CancellationToken stoppingToken)
    {
        var pendentes = await repository.GetHomiliesPendingTranslationAsync(stoppingToken);
        if (pendentes.Count == 0)
        {
            return;
        }

        logger.LogInformation("Tentando traduzir {Quantidade} homilia(s) pendente(s) de tradução.", pendentes.Count);

        foreach (var homily in pendentes)
        {
            var result = await translationService.TranslateAsync(homily.TextoOriginal, homily.OriginalLanguage, stoppingToken);
            homily.TranslationAttempts++;

            if (result.Success)
            {
                homily.TextoTraduzido = result.TranslatedText;
                homily.Status = TranslationStatus.Concluida;
                logger.LogInformation("Homilia '{Titulo}' traduzida com sucesso na tentativa {Tentativa}.", homily.Title, homily.TranslationAttempts);
            }
            else
            {
                homily.Status = TranslationStatus.FalhouTentativa;
                logger.LogWarning(
                    "Tentativa {Tentativa} de traduzir a homilia '{Titulo}' falhou: {Erro}",
                    homily.TranslationAttempts,
                    homily.Title,
                    result.ErrorMessage);
            }
        }

        await repository.SaveChangesAsync(stoppingToken);
    }

    private async Task ApplyPendingMigrationsAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScriptoriumDbContext>();

        logger.LogInformation("Verificando/aplicando migrations pendentes do banco de dados...");
        await dbContext.Database.MigrateAsync(stoppingToken);
        logger.LogInformation("Banco de dados pronto.");
    }
}
