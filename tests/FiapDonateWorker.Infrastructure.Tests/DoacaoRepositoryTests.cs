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
    public async Task ProcessarDoacaoAsync_CampanhaAtiva_CreditaERetornaNovoTotal()
    {
        var nomeBanco = Guid.NewGuid().ToString();
        var campanhaId = Guid.NewGuid();

        await using (var contexto = CriarContexto(nomeBanco))
        {
            contexto.Campanhas.Add(new Campanha { Id = campanhaId, Status = CampanhaStatus.Ativa, ValorArrecadado = 0m });
            await contexto.SaveChangesAsync();
        }

        ResultadoProcessamentoDoacao resultado;
        await using (var contexto = CriarContexto(nomeBanco))
        {
            var repositorio = new DoacaoRepository(contexto);
            resultado = await repositorio.ProcessarDoacaoAsync(
                Guid.NewGuid(), campanhaId, 75m, DateTimeOffset.UtcNow);
        }

        Assert.True(resultado.Processada);
        Assert.True(resultado.Creditada);
        Assert.Equal(campanhaId, resultado.IdCampanha);
        Assert.Equal(75m, resultado.ValorArrecadadoAtual);

        await using (var contexto = CriarContexto(nomeBanco))
        {
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

        ResultadoProcessamentoDoacao resultado;
        await using (var contexto = CriarContexto(nomeBanco))
        {
            var repositorio = new DoacaoRepository(contexto);
            resultado = await repositorio.ProcessarDoacaoAsync(doacaoId, campanhaId, 75m, DateTimeOffset.UtcNow);
        }

        Assert.False(resultado.Processada);

        await using (var contexto = CriarContexto(nomeBanco))
        {
            var campanha = await contexto.Campanhas.SingleAsync(c => c.Id == campanhaId);
            Assert.Equal(75m, campanha.ValorArrecadado);
        }
    }

    [Fact]
    public async Task ProcessarDoacaoAsync_CampanhaInexistente_CriaCampanhaAtivaECredita()
    {
        // A API só publica DoacaoRecebidaEvent para campanhas ativas (validação de
        // atividade feita no intake, em DonationService.RegistrarIntencaoAsync).
        // Por isso o Worker cria a réplica local da campanha como Ativa na primeira
        // doação recebida e credita o valor, em vez de rejeitar por "campanha ausente".
        var nomeBanco = Guid.NewGuid().ToString();
        var campanhaId = Guid.NewGuid();

        await using var contexto = CriarContexto(nomeBanco);
        var repositorio = new DoacaoRepository(contexto);

        var resultado = await repositorio.ProcessarDoacaoAsync(
            Guid.NewGuid(), campanhaId, 75m, DateTimeOffset.UtcNow);

        Assert.True(resultado.Processada);
        Assert.True(resultado.Creditada);
        Assert.Equal(75m, resultado.ValorArrecadadoAtual);

        var campanha = await contexto.Campanhas.SingleAsync(c => c.Id == campanhaId);
        Assert.Equal(CampanhaStatus.Ativa, campanha.Status);
        Assert.Equal(75m, campanha.ValorArrecadado);
        var doacao = await contexto.Doacoes.SingleAsync();
        Assert.Equal(DoacaoStatus.Creditada, doacao.Status);
    }
}
