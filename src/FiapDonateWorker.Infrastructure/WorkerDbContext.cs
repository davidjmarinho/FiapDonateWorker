using FiapDonateWorker.Domain;
using Microsoft.EntityFrameworkCore;

namespace FiapDonateWorker.Infrastructure;

public class WorkerDbContext : DbContext
{
    public WorkerDbContext(DbContextOptions<WorkerDbContext> options) : base(options)
    {
    }

    public DbSet<Campanha> Campanhas => Set<Campanha>();
    public DbSet<Doacao> Doacoes => Set<Doacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Campanha>(entity =>
        {
            // Campanhas é criada pelas migrations do repositório da API; aqui mapeamos
            // apenas as colunas necessárias e excluímos a tabela das migrations deste projeto.
            entity.ToTable("Campanhas", t => t.ExcludeFromMigrations());
            entity.HasKey(c => c.Id);

            // Contrato entre repositórios: o repositório da API grava "Status" como
            // texto puro usando exatamente os nomes dos membros do enum CampanhaStatus
            // ("Ativa", "Concluida", "Cancelada"), sem variação de caixa e sem usar um
            // vocabulário diferente ou o valor inteiro do enum. Qualquer divergência faz
            // com que a leitura falhe silenciosamente em reconhecer a campanha como ativa
            // (HasConversion<string>() abaixo não lança erro em caso de valor
            // desconhecido no sentido esperado por este código - ver DoacaoProcessor),
            // rejeitando todas as doações daquela campanha sem nenhum aviso.
            entity.Property(c => c.Status).HasConversion<string>();

            // Token de concorrência otimista: usa a própria coluna de negócio
            // ValorArrecadado (em vez de uma coluna de sistema específica de
            // provider, como o "xmin" do PostgreSQL) para proteger o incremento
            // contra lost updates quando múltiplas instâncias do consumer
            // processam doações concorrentes para a mesma campanha - ver retry em
            // DoacaoRepository.ProcessarDoacaoAsync. Funciona igual em qualquer
            // provider (PostgreSQL, SQL Server, etc.) e não exige nenhuma coluna
            // extra na tabela Campanhas, que não é dona deste projeto.
            // decimal(18,2): precisão combinada com o mapeamento de MetaFinanceira/
            // ValorArrecadado no repositório da API (FiapDonateCampaign.AppDbContext) -
            // sem isso o SQL Server usa decimal(18,0) por padrão e trunca os centavos.
            entity.Property(c => c.ValorArrecadado)
                .HasColumnType("decimal(18,2)")
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<Doacao>(entity =>
        {
            entity.ToTable("Doacoes");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Status).HasConversion<string>();
            entity.Property(d => d.ValorDoacao).HasColumnType("decimal(18,2)");
        });
    }
}
