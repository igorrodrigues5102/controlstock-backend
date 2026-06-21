namespace ControlStockBackend.Models;

public class MovimentacaoAuditoria
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public string Observacao { get; set; } = string.Empty;
}