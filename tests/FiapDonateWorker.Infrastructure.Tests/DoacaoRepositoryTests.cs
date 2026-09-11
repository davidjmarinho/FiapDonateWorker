using FiapDonateWorker.Domain;
using Microsoft.EntityFrameworkCore;

namespace FiapDonateWorker.Infrastructure.Tests;

public class DoacaoRepositoryTests
{
    private static WorkerDbContext CriarContexto(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<WorkerDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new WorkerDbContext(options);
    }

    [Fact]
    public async Task ProcessarDoacaoAsync_CampanhaAtiva_CreditaValorEPersisteDoacao()
    {
        var nomeBanco = Guid.NewGuid().ToString();
        var campanhaId = Guid.NewGuid();

        await using (var contexto = CriarContexto(nomeBanco))
        {
            contexto.Campanhas.Add(new Campanha { Id = campanhaId, Status = CampanhaStatus.Ativa, ValorArrecadado = 0m });
            await contexto.SaveChangesAsync();
        }

        bool processada;
        await using (var contexto = CriarContexto(nomeBanco))
        {
            var repositorio = new DoacaoRepository(contexto);
            processada = await repositorio.ProcessarDoacaoAsync(
                Guid.NewGuid(), campanhaId, 75m, DateTimeOffset.UtcNow);
        }

        await using (var contexto = CriarContexto(nomeBanco))
        {
            Assert.True(processada);
            var campanha = await contexto.Campanhas.SingleAsync(c => c.Id == campanhaId);
            Assert.Equal(75m, campanha.ValorArrecadado);
            var doacao = await contexto.Doacoes.SingleAsync();
            Assert.Equal(DoacaoStatus.Creditada, doacao.Status);
        }
    }

    [Fact]
    public async Task ProcessarDoacaoAsync_DoacaoJaProcessada_NaoCreditaNovamente()
    {
        var nomeBanco = Guid.NewGuid().ToString();
        var campanhaId = Guid.NewGuid();
        var doacaoId = Guid.NewGuid();

        await using (var contexto = CriarContexto(nomeBanco))
        {
            contexto.Campanhas.Add(new Campanha { Id = campanhaId, Status = CampanhaStatus.Ativa, ValorArrecadado = 0m });
            await contexto.SaveChangesAsync();
        }

        await using (var contexto = CriarContexto(nomeBanco))
        {
            var repositorio = new DoacaoRepository(contexto);
            await repositorio.ProcessarDoacaoAsync(doacaoId, campanhaId, 75m, DateTimeOffset.UtcNow);
        }

        bool processadaDeNovo;
        await using (var contexto = CriarContexto(nomeBanco))
        {
            var repositorio = new DoacaoRepository(contexto);
            processadaDeNovo = await repositorio.ProcessarDoacaoAsync(doacaoId, campanhaId, 75m, DateTimeOffset.UtcNow);
        }

        await using (var contexto = CriarContexto(nomeBanco))
        {
            Assert.False(processadaDeNovo);
            var campanha = await contexto.Campanhas.SingleAsync(c => c.Id == campanhaId);
            Assert.Equal(75m, campanha.ValorArrecadado);
        }
    }

    [Fact]
    public async Task ProcessarDoacaoAsync_CampanhaInexistente_RegistraDoacaoRejeitada()
    {
        var nomeBanco = Guid.NewGuid().ToString();

        await using var contexto = CriarContexto(nomeBanco);
        var repositorio = new DoacaoRepository(contexto);

        var processada = await repositorio.ProcessarDoacaoAsync(
            Guid.NewGuid(), Guid.NewGuid(), 75m, DateTimeOffset.UtcNow);

        Assert.True(processada);
        var doacao = await contexto.Doacoes.SingleAsync();
        Assert.Equal(DoacaoStatus.Rejeitada, doacao.Status);
    }
}
