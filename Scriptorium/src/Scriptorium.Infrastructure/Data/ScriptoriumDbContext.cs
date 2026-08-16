using Microsoft.EntityFrameworkCore;
using Scriptorium.Domain.Entities;

namespace Scriptorium.Infrastructure.Data;

/// <summary>
/// O DbContext é a "porta de entrada" do Entity Framework Core para o banco
/// SQLite: representa uma sessão com o banco de dados e expõe cada tabela
/// como uma propriedade <see cref="DbSet{TEntity}"/>.
///
/// COMO RODAR AS MIGRATIONS (guia de estudo — leia com atenção!):
///
/// O EF Core usa "Code-First Migrations": você escreve as classes C# (as
/// entidades em Scriptorium.Domain.Entities) e o EF gera automaticamente o
/// SQL necessário para criar/alterar as tabelas do banco para bater com o
/// seu código. Isso é o oposto de "Database-First" (onde você desenharia
/// as tabelas primeiro e geraria classes a partir delas).
///
/// Passo a passo (rodar sempre a partir da pasta Scriptorium/, ou seja,
/// a raiz da solução):
///
/// 1) Instalar a ferramenta global do EF Core (só precisa fazer 1 vez por
///    máquina de desenvolvimento):
///    <c>dotnet tool install --global dotnet-ef</c>
///
/// 2) Criar a migration inicial (isso gera arquivos C# dentro de
///    src/Scriptorium.Infrastructure/Data/Migrations/, descrevendo o
///    "diff" entre o banco vazio e o modelo atual):
///    <c>dotnet ef migrations add InitialCreate --project src/Scriptorium.Infrastructure --startup-project src/Scriptorium.API</c>
///
///    Por que "--startup-project src/Scriptorium.API"? Porque o EF Core
///    precisa executar o Program.cs de ALGUM projeto executável para
///    descobrir a connection string e montar o DbContext configurado (ele
///    literalmente inicia a aplicação por baixo dos panos, com um ambiente
///    especial). O projeto Infrastructure é uma biblioteca (não tem
///    Program.cs próprio), por isso apontamos para a API como o projeto
///    "de inicialização".
///
/// 3) Aplicar a migration ao banco SQLite (cria de fato o arquivo .db e as
///    tabelas nele, executando o SQL gerado no passo 2):
///    <c>dotnet ef database update --project src/Scriptorium.Infrastructure --startup-project src/Scriptorium.API</c>
///
/// 4) Sempre que você alterar uma entidade (adicionar uma propriedade nova,
///    por exemplo), repita o passo 2 com um nome novo e descritivo
///    (ex: "AddImageUrlToSaint") e depois o passo 3 de novo.
///
/// NA PRÁTICA, EM PRODUÇÃO (dentro do container Docker): o Program.cs da
/// API chama <c>dbContext.Database.Migrate()</c> automaticamente na
/// inicialização (veja Scriptorium.API/Program.cs), então você não precisa
/// rodar `dotnet ef database update` manualmente dentro do container — as
/// migrations geradas em tempo de desenvolvimento são aplicadas sozinhas
/// quando o container sobe.
/// </summary>
public class ScriptoriumDbContext(DbContextOptions<ScriptoriumDbContext> options) : DbContext(options)
{
    /// <summary>Tabela de devocionais diários — a raiz do agregado.</summary>
    public DbSet<DailyDevotional> DailyDevotionals => Set<DailyDevotional>();

    /// <summary>Tabela de leituras bíblicas (1-para-N com DailyDevotional).</summary>
    public DbSet<Reading> Readings => Set<Reading>();

    /// <summary>Tabela de santos do dia (1-para-1 com DailyDevotional).</summary>
    public DbSet<SaintOfTheDay> Saints => Set<SaintOfTheDay>();

    /// <summary>Tabela de homilias do Papa.</summary>
    public DbSet<Homily> Homilies => Set<Homily>();

    /// <summary>
    /// Aqui usamos a "Fluent API" do EF Core para configurar detalhes do
    /// mapeamento objeto-relacional que não dá para (ou não deveria)
    /// expressar apenas com Data Annotations nas entidades — mantendo as
    /// entidades de Domain "limpas", sem nenhuma referência ao
    /// Entity Framework (outra aplicação da Regra da Dependência da Clean
    /// Architecture: Domain não deveria conhecer detalhes de persistência).
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DailyDevotional>(entity =>
        {
            // Cada dia só pode ter UM devocional — índice único na data
            // garante isso no nível do banco (defesa em profundidade: além
            // da lógica do repositório, o próprio banco recusa duplicatas).
            entity.HasIndex(d => d.Date).IsUnique();

            // Relação 1-para-N: um devocional tem várias leituras. Se o
            // devocional for apagado, suas leituras são apagadas em cascata
            // (não faz sentido uma Reading existir "órfã", sem seu dia).
            entity.HasMany(d => d.Readings)
                  .WithOne(r => r.DailyDevotional)
                  .HasForeignKey(r => r.DailyDevotionalId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Relação 1-para-1 com o santo do dia.
            entity.HasOne(d => d.Saint)
                  .WithOne(s => s.DailyDevotional)
                  .HasForeignKey<SaintOfTheDay>(s => s.DailyDevotionalId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Relação 1-para-1 (opcional) com a homilia. Usamos SetNull em
            // vez de Cascade aqui: se o devocional for apagado, a homilia
            // em si NÃO deveria ser apagada (ela pode ser referenciada/
            // reaproveitada de forma independente) — apenas o vínculo com
            // aquele dia é desfeito.
            entity.HasOne(d => d.Homily)
                  .WithOne(h => h.DailyDevotional)
                  .HasForeignKey<Homily>(h => h.DailyDevotionalId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Homily>(entity =>
        {
            // Evita salvar a mesma homilia duas vezes (identificada pela
            // URL de origem, que é naturalmente única por publicação).
            entity.HasIndex(h => h.SourceUrl).IsUnique();
        });
    }
}
