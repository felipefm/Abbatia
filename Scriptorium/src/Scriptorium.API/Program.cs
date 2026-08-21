using Microsoft.EntityFrameworkCore;
using Scriptorium.API.Endpoints;
using Scriptorium.Application;
using Scriptorium.Infrastructure;
using Scriptorium.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Registra tudo que a camada Application precisa (hoje: o
// DevotionalBuilderService — usado aqui apenas indiretamente, caso algum
// endpoint futuro precise forçar um reprocessamento manual de um dia; a API
// em si só lê do repositório, mas mantemos as camadas registradas de forma
// consistente entre API e Worker).
builder.Services.AddScriptoriumApplication();

// Registra tudo da camada Infrastructure: DbContext (SQLite), scrapers,
// serviço de tradução (LM Studio) e o repositório. Ver comentários
// detalhados em Scriptorium.Infrastructure/ServiceCollectionExtensions.cs.
builder.Services.AddScriptoriumInfrastructure(builder.Configuration);

// ----------------------------------------------------------------------
// SWAGGER / OpenAPI — TELA DE TESTE VISUAL DA API.
// ----------------------------------------------------------------------
// Minimal APIs (usadas em DevotionalEndpoints.cs) não têm, por padrão,
// nenhum jeito de "descobrir" quais rotas existem sem ler o código-fonte.
// O pacote Swashbuckle.AspNetCore resolve isso em duas partes:
//
// 1) AddEndpointsApiExplorer(): registra o serviço que INSPECIONA todos os
//    endpoints mapeados (app.MapGet, etc) em tempo de execução via
//    reflection, descobrindo rotas, parâmetros e tipos de retorno mesmo sem
//    Controllers/atributos — é o que permite Minimal APIs aparecerem no
//    Swagger sem nenhuma anotação extra além do que já usamos
//    (.WithSummary, .Produces<T>() em DevotionalEndpoints.cs).
// 2) AddSwaggerGen(): gera o documento OpenAPI (um JSON descrevendo toda a
//    API) a partir dessas informações descobertas.
//
// Depois, já com "app" montado, UseSwagger() expõe esse JSON em
// /swagger/v1/swagger.json, e UseSwaggerUI() serve a PÁGINA HTML
// interativa (Swagger UI) que lê esse JSON e desenha os formulários de
// teste — é essa página que você vai acessar no navegador.
// ----------------------------------------------------------------------
// CORS — necessário porque a API e o Oratorium (frontend) são dois
// ORIGENS diferentes do ponto de vista do navegador (portas 8110 e 8111,
// mesmo estando no mesmo host físico). Sem isso, o navegador do usuário
// final BLOQUEIA a leitura da resposta no JavaScript do Oratorium mesmo
// quando a API responde 200 OK com o JSON correto — é um bloqueio do
// PRÓPRIO NAVEGADOR (Same-Origin Policy), não um erro do servidor, então
// nem aparece nos logs da API (só no DevTools do navegador, como "CORS
// Missing Allow Origin").
//
// Usamos AllowAnyOrigin (em vez de uma lista de origens específicas) de
// propósito: essa API só expõe dados de LEITURA pública (sem
// autenticação, sem cookies, sem nada sensível por usuário), então não há
// risco de segurança em liberar qualquer origem — e evita repetir a
// mesma classe de bug já vista neste projeto (um IP/endereço hardcoded
// que precisa ser mantido sincronizado em vários lugares; ver Bug #2 em
// docs/04-inteligencia-de-codigo.md). AllowCredentials() NÃO é usado
// junto de AllowAnyOrigin (o navegador proíbe essa combinação), o que é
// coerente com a API não usar cookies/sessão de qualquer forma.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Abbatia — Scriptorium API",
        Version = "v1",
        Description = "API de leitura do devocional católico diário (Santo do Dia, Liturgia e Homilia do Papa), " +
                      "populada de madrugada pelo Scriptorium.Worker a partir das fontes reais na web.",
    });
});

var app = builder.Build();

// Precisa vir ANTES do mapeamento dos endpoints (MapGet/MapDevotionalEndpoints
// abaixo) — é o middleware que efetivamente adiciona os cabeçalhos
// Access-Control-Allow-* nas respostas.
app.UseCors();

// APLICA MIGRATIONS PENDENTES NA INICIALIZAÇÃO.
// Mesmo a API (que só faz leitura) precisa disso: é perfeitamente possível,
// num ambiente Docker Compose, que o container da API suba ANTES do
// container do Worker ter tido a chance de criar o banco. Sem isso, a
// primeira consulta à API falharia com um erro de "tabela não existe".
// Chamar Migrate() aqui é idempotente (não faz nada se já estiver tudo em
// dia), então é seguro chamar em AMBOS os processos (API e Worker) toda vez
// que sobem.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ScriptoriumDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Ativa o middleware que serve o JSON do OpenAPI e a página HTML do Swagger UI.
//
// DECISÃO: deixamos o Swagger disponível SEMPRE (não só quando
// app.Environment.IsDevelopment(), como é comum em templates padrão do
// ASP.NET Core). Isso é intencional: a Abbatia é uma ferramenta pessoal de
// homelab, rodando dentro da sua rede local (não exposta na internet
// pública), então a conveniência de poder testar a API direto do navegador
// em produção pesa mais do que o risco de segurança — que, num serviço
// público de verdade, faria sentido restringir. Se um dia você expuser essa
// API na internet, vale reconsiderar essa escolha (ex: proteger /swagger
// atrás de autenticação, ou voltar a restringir a IsDevelopment()).
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Scriptorium API v1");

    // Deixa a Swagger UI disponível na raiz do site (http://SEU_IP:8110/)
    // em vez do caminho padrão "/swagger/index.html" — um endereço mais
    // curto e fácil de lembrar/favoritar.
    options.RoutePrefix = string.Empty;
});

// Endpoint simples de verificação de saúde — usado pelo HEALTHCHECK do
// Dockerfile/docker-compose.yml para o Docker saber se o container da API
// já está pronto para receber tráfego (e reiniciá-lo automaticamente se
// parar de responder).
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("HealthCheck")
    .ExcludeFromDescription();

// Registra as rotas /api/devotional/today e /api/devotional/{date}
// (implementadas em Scriptorium.API/Endpoints/DevotionalEndpoints.cs).
app.MapDevotionalEndpoints();

// Registra /api/devotional/calendar/{year}/{month} (calendário mensal).
app.MapCalendarEndpoints();

// Registra o CRUD /api/diary/{date} (diário espiritual pessoal).
app.MapDiaryEndpoints();

app.Run();
