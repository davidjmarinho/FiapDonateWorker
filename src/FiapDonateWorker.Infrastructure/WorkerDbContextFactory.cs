using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FiapDonateWorker.Infrastructure;

public class WorkerDbContextFactory : IDesignTimeDbContextFactory<WorkerDbContext>
{
    public WorkerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("WORKER_DB_CONNECTION")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__WorkerDb")
            ?? "Server=localhost,1433;Database=conexao_solidaria;User Id=sa;TrustServerCertificate=True;";

        var optionsBuilder = new DbContextOptionsBuilder<WorkerDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new WorkerDbContext(optionsBuilder.Options);
    }
}
