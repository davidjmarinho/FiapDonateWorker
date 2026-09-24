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
            // Este Worker é dono da sua própria réplica local da campanha (arquitetura
            // database-per-service). A tabela Campanhas da API (fonte da verdade) NÃO é
            // compartilhada: a réplica é populada pelos eventos recebidos e mantida pelas
            // migrations deste projeto.
            entity.ToTable("Campanhas");
            entity.HasKey(c => c.Id);

            // Status é persistido como texto usando exatamente os nomes dos membros do
            // enum CampanhaStatus ("Ativa", "Concluida", "Cancelada").
            entity.Property(c => c.Status).HasConversion<string>();

            // Token de concorrência otimista: usa a própria coluna de negócio
            // ValorArrecadado para proteger o incremento contra lost updates quando
            // múltiplas instâncias do consumer processam doações concorrentes para a
            // mesma campanha - ver retry em DoacaoRepository.ProcessarDoacaoAsync.
            // decimal(18,2): mantém a precisão de centavos (sem isso o SQL Server usa
            // decimal(18,0) por padrão e trunca os centavos).
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
