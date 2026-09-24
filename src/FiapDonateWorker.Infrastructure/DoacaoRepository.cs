using FiapDonateWorker.Domain;
using Microsoft.EntityFrameworkCore;

namespace FiapDonateWorker.Infrastructure;

public class DoacaoRepository
{
    private readonly WorkerDbContext _dbContext;

    public DoacaoRepository(WorkerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ResultadoProcessamentoDoacao> ProcessarDoacaoAsync(
        Guid doacaoId,
        Guid idCampanha,
        decimal valorDoacao,
        DateTimeOffset dataHoraRecebida,
        CancellationToken cancellationToken = default)
    {
        var jaProcessada = await _dbContext.Doacoes
            .AnyAsync(d => d.Id == doacaoId, cancellationToken);

        if (jaProcessada)
        {
            return new ResultadoProcessamentoDoacao(
                Processada: false,
                Creditada: false,
                IdCampanha: idCampanha,
                ValorArrecadadoAtual: 0m);
        }

        var campanha = await _dbContext.Campanhas
            .SingleOrDefaultAsync(c => c.Id == idCampanha, cancellationToken);

        if (campanha is null)
        {
            // Este Worker é dono apenas de uma réplica local da campanha. A tabela
            // Campanhas da API (fonte da verdade) NÃO é compartilhada. A API só
            // publica DoacaoRecebidaEvent para campanhas ativas (validação de
            // atividade no intake, em DonationService.RegistrarIntencaoAsync), então
            // ao ver uma campanha ainda desconhecida criamos a réplica local como
            // Ativa e creditamos - em vez de rejeitar por "campanha ausente".
            campanha = new Campanha
            {
                Id = idCampanha,
                Status = CampanhaStatus.Ativa,
                ValorArrecadado = 0m
            };
            _dbContext.Campanhas.Add(campanha);
        }

        var doacao = new Doacao
        {
            Id = doacaoId,
            IdCampanha = idCampanha,
            ValorDoacao = valorDoacao,
            DataHoraRecebida = dataHoraRecebida,
            DataHoraProcessada = DateTimeOffset.UtcNow
        };

        DoacaoProcessor.Processar(campanha, doacao);

        var doacaoCreditada = doacao.Status == DoacaoStatus.Creditada;

        _dbContext.Doacoes.Add(doacao);

        const int maxTentativas = 3;
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateConcurrencyException) when (tentativa < maxTentativas)
            {
                // Outra instância do consumer alterou Campanha.ValorArrecadado
                // (token de concorrência otimista) entre a leitura e o SaveChanges.
                // Recarrega o valor atual do banco e reaplica apenas o incremento
                // numérico decidido por DoacaoProcessor.Processar - não reavaliamos
                // o status da doação, pois mudança de status de campanha em voo é um
                // caso já tratado separadamente (não uma corrida a ser resolvida aqui).
                var campanhaEntry = _dbContext.Entry(campanha);
                await campanhaEntry.ReloadAsync(cancellationToken);

                if (doacaoCreditada)
                {
                    campanha.ValorArrecadado += doacao.ValorDoacao;
                }
            }
        }

        return new ResultadoProcessamentoDoacao(
            Processada: true,
            Creditada: doacaoCreditada,
            IdCampanha: idCampanha,
            ValorArrecadadoAtual: campanha.ValorArrecadado);
    }
}
