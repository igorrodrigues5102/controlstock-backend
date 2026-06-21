namespace ControlStockBackend.Models;

public class Produto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int QuantidadeAtual { get; set; }
    public int EstoqueMinimo { get; set; }
    public string Descricao { get; set; } = string.Empty;

    // Mudança aqui: O produto agora tem uma lista de fotos vinculadas
    public List<FotoProduto> Fotos { get; set; } = new();
}