using FiapDonateWorker.Infrastructure;
using FiapDonateWorker.Api.Events;
using MassTransit;
using Prometheus;

namespace FiapDonateWorker.Api.Consumers;

public class DoacaoRecebidaConsumer : IConsumer<DoacaoRecebidaEvent>
{
    private static readonly Counter DoacoesProcessadas = Metrics.CreateCounter(
        "worker_doacoes_processadas_total",
        "Quantidade de doacoes processadas pelo worker, particionadas por resultado.",
        new CounterConfiguration { LabelNames = new[] { "resultado" } });

    private readonly DoacaoRepository _repositorio;
    private readonly ILogger<DoacaoRecebidaConsumer> _logger;

    public DoacaoRecebidaConsumer(DoacaoRepository repositorio, ILogger<DoacaoRecebidaConsumer> logger)
    {
        _repositorio = repositorio;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DoacaoRecebidaEvent> context)
    {
        var evento = context.Message;

        var resultado = await _repositorio.ProcessarDoacaoAsync(
            evento.DoacaoId,
            evento.IdCampanha,
            evento.ValorDoacao,
            evento.DataHoraRecebida,
            context.CancellationToken);

        if (!resultado.Processada)
        {
            _logger.LogInformation(
                "Doacao {DoacaoId} ja havia sido processada anteriormente, ignorando duplicata.",
                evento.DoacaoId);
            DoacoesProcessadas.WithLabels("duplicada").Inc();
            return;
        }

        if (resultado.Creditada)
        {
            // Devolve à API (dona da campanha) o valor total arrecadado atual, para que
            // ela atualize seu proprio Campaigns.ValorArrecadado e o Painel de
            // Transparencia reflita a doacao processada. Valor absoluto = consumo
            // idempotente do lado da API.
            await context.Publish(new ValorArrecadadoAtualizadoEvent(
                IdCampanha: resultado.IdCampanha,
                ValorArrecadado: resultado.ValorArrecadadoAtual,
                AtualizadoEm: DateTimeOffset.UtcNow));
        }

        DoacoesProcessadas.WithLabels("processada").Inc();
    }
}
