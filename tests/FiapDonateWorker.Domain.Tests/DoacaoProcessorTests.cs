using FiapDonateWorker.Domain;

namespace FiapDonateWorker.Domain.Tests;

public class DoacaoProcessorTests
{
    [Fact]
    public void Processar_CampanhaAtiva_CreditaDoacaoEAtualizaValorArrecadado()
    {
        var campanha = new Campanha
        {
            Id = Guid.NewGuid(),
            Status = CampanhaStatus.Ativa,
            ValorArrecadado = 100m
        };
        var doacao = new Doacao
        {
            Id = Guid.NewGuid(),
            IdCampanha = campanha.Id,
            ValorDoacao = 50m
        };

        DoacaoProcessor.Processar(campanha, doacao);

        Assert.Equal(DoacaoStatus.Creditada, doacao.Status);
        Assert.Equal(150m, campanha.ValorArrecadado);
    }

    [Theory]
    [InlineData(CampanhaStatus.Cancelada)]
    [InlineData(CampanhaStatus.Concluida)]
    public void Processar_CampanhaNaoAtiva_RejeitaDoacaoENaoAlteraValorArrecadado(CampanhaStatus status)
    {
        var campanha = new Campanha
        {
            Id = Guid.NewGuid(),
            Status = status,
            ValorArrecadado = 100m
        };
        var doacao = new Doacao
        {
            Id = Guid.NewGuid(),
            IdCampanha = campanha.Id,
            ValorDoacao = 50m
        };

        DoacaoProcessor.Processar(campanha, doacao);

        Assert.Equal(DoacaoStatus.Rejeitada, doacao.Status);
        Assert.Equal(100m, campanha.ValorArrecadado);
    }

    [Fact]
    public void Processar_CampanhaInexistente_RejeitaDoacao()
    {
        var doacao = new Doacao
        {
            Id = Guid.NewGuid(),
            IdCampanha = Guid.NewGuid(),
            ValorDoacao = 50m
        };

        DoacaoProcessor.Processar(null, doacao);

        Assert.Equal(DoacaoStatus.Rejeitada, doacao.Status);
    }
}
