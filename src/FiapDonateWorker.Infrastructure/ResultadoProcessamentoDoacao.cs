namespace FiapDonateWorker.Infrastructure;

/// <summary>
/// Resultado do processamento de uma doação pelo Worker.
/// </summary>
/// <param name="Processada">
/// <c>false</c> quando a doação já havia sido processada antes (duplicata idempotente);
/// <c>true</c> quando foi processada agora.
/// </param>
/// <param name="Creditada">
/// <c>true</c> quando o valor foi creditado na campanha; <c>false</c> quando a doação
/// foi rejeitada (campanha não ativa).
/// </param>
/// <param name="IdCampanha">Identificador da campanha afetada.</param>
/// <param name="ValorArrecadadoAtual">
/// Valor total arrecadado da campanha após o processamento. Só é significativo quando
/// <see cref="Processada"/> e <see cref="Creditada"/> são <c>true</c>; usado pelo consumer
/// para devolver o valor atualizado à API (evento ValorArrecadadoAtualizadoEvent).
/// </param>
public record ResultadoProcessamentoDoacao(
    bool Processada,
    bool Creditada,
    Guid IdCampanha,
    decimal ValorArrecadadoAtual);
