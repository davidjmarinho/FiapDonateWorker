namespace FiapDonateWorker.Domain;

public static class DoacaoProcessor
{
    public static void Processar(Campanha? campanha, Doacao doacao)
    {
        doacao.Status = AvaliarStatus(campanha);

        if (doacao.Status == DoacaoStatus.Creditada && campanha is not null)
        {
            campanha.ValorArrecadado += doacao.ValorDoacao;
        }
    }

    private static DoacaoStatus AvaliarStatus(Campanha? campanha)
    {
        if (campanha is null)
        {
            return DoacaoStatus.Rejeitada;
        }

        return campanha.Status == CampanhaStatus.Ativa
            ? DoacaoStatus.Creditada
            : DoacaoStatus.Rejeitada;
    }
}
