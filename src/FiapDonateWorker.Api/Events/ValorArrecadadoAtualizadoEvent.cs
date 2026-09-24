namespace FiapDonateWorker.Api.Events;

/// <summary>
/// Evento publicado pelo Worker após creditar uma doação, devolvendo à API o valor
/// total arrecadado atual da campanha (arquitetura database-per-service: a API é dona
/// da campanha e atualiza o próprio ValorArrecadado ao consumir este evento).
///
/// O valor é ABSOLUTO (total corrente), não um delta, o que torna o consumo idempotente
/// na API: reentregas da mesma mensagem apenas reafirmam o mesmo total, sem somar duas vezes.
///
/// Contrato entre repositórios: a API deve consumir um tipo com o mesmo namespace + nome
/// (ou compartilhar este contrato), pois o MassTransit roteia mensagens pelo nome
/// namespace-qualificado do tipo.
/// </summary>
public record ValorArrecadadoAtualizadoEvent(
    Guid IdCampanha,
    decimal ValorArrecadado,
    DateTimeOffset AtualizadoEm);
