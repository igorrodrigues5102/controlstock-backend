namespace ControlStockBackend.Models;

public record LoginRequest(string Email, string Senha);
public record ItemPedidoRequest(int Id, string Nome, int Qtd);
public record PedidoRequest(string NomeCliente, string DocumentoCliente, string FormaPagamento, List<ItemPedidoRequest> Itens);
public record LoteRequest(int ProdutoId, int Quantidade, string Observacao);
public record PrecoRequest(int ProdutoId, decimal NovoPrecoBase, decimal PrecoPromo);
public record CadastroProdutoRequest(string nome, decimal preco, int estoqueInicial, string url1, string url2, string url3);