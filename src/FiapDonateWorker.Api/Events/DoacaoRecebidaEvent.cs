namespace FiapDonateWorker.Api.Events;

public record DoacaoRecebidaEvent(
    Guid DoacaoId,
    Guid IdCampanha,
    decimal ValorDoacao,
    DateTimeOffset DataHoraRecebida);
