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

        var processada = await _repositorio.ProcessarDoacaoAsync(
            evento.DoacaoId,
            evento.IdCampanha,
            evento.ValorDoacao,
            evento.DataHoraRecebida,
            context.CancellationToken);

        if (!processada)
        {
            _logger.LogInformation(
                "Doacao {DoacaoId} ja havia sido processada anteriormente, ignorando duplicata.",
                evento.DoacaoId);
            DoacoesProcessadas.WithLabels("duplicada").Inc();
            return;
        }

        DoacoesProcessadas.WithLabels("processada").Inc();
    }
}
