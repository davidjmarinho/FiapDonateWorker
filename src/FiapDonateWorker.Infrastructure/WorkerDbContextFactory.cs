using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FiapDonateWorker.Infrastructure;

public class WorkerDbContextFactory : IDesignTimeDbContextFactory<WorkerDbContext>
{
    public WorkerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("WORKER_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=conexao_solidaria;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<WorkerDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new WorkerDbContext(optionsBuilder.Options);
    }
}
