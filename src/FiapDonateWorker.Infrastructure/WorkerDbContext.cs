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

            // Token de concorrência otimista usando a coluna de sistema "xmin" do
            // PostgreSQL (já existe em toda tabela, não requer migration). Protege o
            // incremento de ValorArrecadado contra lost updates quando múltiplas
            // instâncias do consumer processam doações concorrentes para a mesma
            // campanha - ver retry em DoacaoRepository.ProcessarDoacaoAsync.
            // (Equivalente a UseXminAsConcurrencyToken(), obsoleto nesta versão do
            // provider Npgsql em favor da API padrão do EF Core abaixo.)
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
        });

        modelBuilder.Entity<Doacao>(entity =>
        {
            entity.ToTable("Doacoes");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Status).HasConversion<string>();
        });
    }
}
