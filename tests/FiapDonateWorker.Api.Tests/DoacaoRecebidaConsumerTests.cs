using FiapDonateWorker.Api.Consumers;
using FiapDonateWorker.Api.Events;
using FiapDonateWorker.Infrastructure;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FiapDonateWorker.Api.Tests;

public class DoacaoRecebidaConsumerTests
{
    [Fact]
    public async Task Consume_DoacaoCreditada_PublicaValorArrecadadoAtualizado()
    {
        var idCampanha = Guid.NewGuid();

        await using var provider = new ServiceCollection()
            .AddDbContext<WorkerDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()))
            .AddScoped<DoacaoRepository>()
            .AddLogging()
            .AddMassTransitTestHarness(x => x.AddConsumer<DoacaoRecebidaConsumer>())
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new DoacaoRecebidaEvent(
                DoacaoId: Guid.NewGuid(),
                IdCampanha: idCampanha,
                ValorDoacao: 120m,
                DataHoraRecebida: DateTimeOffset.UtcNow));

            Assert.True(await harness.Consumed.Any<DoacaoRecebidaEvent>());
            Assert.True(await harness.Published.Any<ValorArrecadadoAtualizadoEvent>());

            var publicado = await harness.Published
                .SelectAsync<ValorArrecadadoAtualizadoEvent>().First();
            Assert.Equal(idCampanha, publicado.Context.Message.IdCampanha);
            Assert.Equal(120m, publicado.Context.Message.ValorArrecadado);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
