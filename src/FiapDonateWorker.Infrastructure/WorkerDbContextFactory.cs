using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FiapDonateWorker.Infrastructure;

public class WorkerDbContextFactory : IDesignTimeDbContextFactory<WorkerDbContext>
{
    public WorkerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("WORKER_DB_CONNECTION")
            ?? "Server=localhost,1433;Database=conexao_solidaria;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;";

        var optionsBuilder = new DbContextOptionsBuilder<WorkerDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new WorkerDbContext(optionsBuilder.Options);
    }
}
