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

    public async Task<bool> ProcessarDoacaoAsync(
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
            return false;
        }

        var campanha = await _dbContext.Campanhas
            .SingleOrDefaultAsync(c => c.Id == idCampanha, cancellationToken);

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
            catch (DbUpdateConcurrencyException) when (tentativa < maxTentativas && campanha is not null)
            {
                // Outra instância do consumer alterou Campanha.ValorArrecadado
                // (token de concorrência "xmin") entre a leitura e o SaveChanges.
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

        return true;
    }
}
