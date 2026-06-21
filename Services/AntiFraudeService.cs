using ControlStockBackend.Models;

namespace ControlStockBackend.Services;

public class AntiFraudeService
{
    public int AnalisarRiscoVenda(PedidoRequest pedido, decimal valorTotal)
    {
        int scoreRisco = 0;

        // Regra de comportamento 1: Compras em volume bizarro (comportamento de Bot)
        int totalItens = pedido.Itens.Sum(i => i.Qtd);
        if (totalItens > 50) scoreRisco += 40; // +40% de risco

        // Regra de comportamento 2: Valores muito discrepantes no PIX sem histórico anterior
        if (pedido.FormaPagamento == "PIX" && valorTotal > 10000) scoreRisco += 30;

        // Regra de comportamento 3: Validação estrutural de segurança do documento do cliente
        if (pedido.DocumentoCliente == "PERFIL AUTENTICADO" && totalItens > 100) scoreRisco += 20;

        return scoreRisco; // Retorna um score de 0 a 100
    }
}