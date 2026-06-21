using System.Text.Json.Serialization;

namespace ControlStockBackend.Models;

public class FotoProduto
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;

    // Chave estrangeira para vincular ao produto correto
    public int ProdutoId { get; set; }

    // Evita loop infinito na hora de transformar o banco em JSON para o front-end
    [JsonIgnore]
    public Produto? Produto { get; set; }
}