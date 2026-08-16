using Scriptorium.Application;
using Scriptorium.Infrastructure;
using Scriptorium.Worker;
using Scriptorium.Worker.Options;

// Host.CreateApplicationBuilder é o ponto de entrada padrão do "Generic
// Host" do .NET para aplicações sem interface web (workers, serviços de
// background, jobs). Ele já vem com Configuration (appsettings.json + env
// vars), Logging e Dependency Injection prontos, exatamente como o
// WebApplication.CreateBuilder da API — só que sem o pipeline HTTP, que não
// faz sentido aqui: este processo nunca recebe requisições, só executa
// tarefas agendadas em background.
var builder = Host.CreateApplicationBuilder(args);

// Reaproveitamos os MESMOS métodos de extensão de DI usados pela API
// (Scriptorium.API/Program.cs) — o Worker e a API compartilham exatamente
// a mesma configuração de banco de dados, scrapers e tradução, o que faz
// muito sentido: ambos operam sobre o mesmo domínio, só que em papéis
// diferentes (o Worker ESCREVE dados raspando a web; a API só LÊ do banco).
builder.Services.AddScriptoriumApplication();
builder.Services.AddScriptoriumInfrastructure(builder.Configuration);

builder.Services.Configure<WorkerScheduleOptions>(
    builder.Configuration.GetSection(WorkerScheduleOptions.SectionName));

// Registra o BackgroundService que efetivamente roda a raspagem diária.
// AddHostedService é o "gancho" que diz ao Generic Host: "quando a
// aplicação iniciar, chame ExecuteAsync desta classe, e mantenha o
// processo vivo enquanto ela estiver rodando".
builder.Services.AddHostedService<DailyDevotionalWorker>();

var host = builder.Build();
host.Run();
