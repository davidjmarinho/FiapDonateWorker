namespace FiapDonateWorker.Domain;

public class Doacao
{
    public Guid Id { get; set; }
    public Guid IdCampanha { get; set; }
    public decimal ValorDoacao { get; set; }
    public DateTimeOffset DataHoraRecebida { get; set; }
    public DateTimeOffset DataHoraProcessada { get; set; }
    public DoacaoStatus Status { get; set; }
}
