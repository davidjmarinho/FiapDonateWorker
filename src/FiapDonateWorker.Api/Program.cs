using FiapDonateWorker.Infrastructure;
using FiapDonateWorker.Api.Consumers;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("WorkerDb")
    ?? throw new InvalidOperationException("ConnectionStrings:WorkerDb nao configurada.");

var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitVirtualHost = builder.Configuration["RabbitMq:VirtualHost"] ?? "/";
var rabbitUsername = builder.Configuration["RabbitMq:Username"] ?? "guest";
var rabbitPassword = builder.Configuration["RabbitMq:Password"] ?? "guest";
var rabbitUri = $"amqp://{rabbitUsername}:{rabbitPassword}@{rabbitHost}{rabbitVirtualHost}";

builder.Services.AddDbContext<WorkerDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<DoacaoRepository>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<DoacaoRecebidaConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, rabbitVirtualHost, h =>
        {
            h.Username(rabbitUsername);
            h.Password(rabbitPassword);
        });

        cfg.ReceiveEndpoint("doacao-recebida-queue", e =>
        {
            e.ConfigureConsumer<DoacaoRecebidaConsumer>(context);
            e.UseMessageRetry(r => r.Immediate(3));
        });
    });
});

// Conexao dedicada ao health check (a conexao interna do MassTransit nao e
// exposta via DI), conforme recomendado pelo README do AspNetCore.HealthChecks.Rabbitmq.
builder.Services.AddSingleton<IConnection>(sp =>
{
    var factory = new ConnectionFactory { Uri = new Uri(rabbitUri) };
    return factory.CreateConnectionAsync().GetAwaiter().GetResult();
});

// Tags "ready" marcam os checks que dependem de infraestrutura externa
// (SQL Server/RabbitMQ). Eles alimentam apenas /health/ready: uma indisponibilidade
// transitória dessas dependências deve tirar o pod de circulação (readiness),
// mas NAO deve derrubar o processo via liveness - reiniciar o pod nao conserta
// uma dependencia externa fora do ar.
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "sqlserver", tags: new[] { "ready" })
    .AddRabbitMQ(name: "rabbitmq", tags: new[] { "ready" });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapGet("/", () => Results.Ok(new { service = "FiapDonateWorker.Api", status = "running" }));

// /health/live: apenas confirma que o processo esta de pe (nenhum check de
// dependencia externa e executado) - usado pela liveness probe.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

// /health/ready: executa os checks marcados com a tag "ready" (SQL Server,
// RabbitMQ) - usado pela readiness probe.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Alias mantido por compatibilidade com integracoes existentes (equivalente a
// /health/ready).
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.UseHttpMetrics();
app.MapMetrics();

app.Run();
