namespace ControlStockBackend.Models;

public class Venda
{
    public int Id { get; set; }
    public string NomeCliente { get; set; } = string.Empty;
    public string DocumentoCliente { get; set; } = string.Empty;
    public string FormaPagamento { get; set; } = string.Empty;
    public DateTime DataVenda { get; set; } = DateTime.Now;
}