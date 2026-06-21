using ControlStockBackend.Data;
using ControlStockBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlStockBackend.Services;

public class EstoqueInteligenteService
{
    private readonly ContextoMarketplace _db;

    public EstoqueInteligenteService(ContextoMarketplace db)
    {
        _db = db;
    }

    // IA Estatística/Preditiva: Calcula a previsão de esgotamento
    public async Task<string> PreverDiasRestantes(int produtoId)
    {
        var produto = await _db.Produtos.FindAsync(produtoId);
        if (produto == null) return "Produto não encontrado";
        if (produto.QuantidadeAtual == 0) return "❌ Esgotado";

        // Busca o histórico de saídas desse produto nos últimos 30 dias
        var dataLimite = DateTime.Now.AddDays(-30);
        var vendasSemanais = await _db.ItensVenda
            .Where(i => i.ProdutoId == produtoId)
            .ToListAsync();

        if (vendasSemanais.Count == 0)
            return " Pouca movimentação para previsão.";

        // Calcula a média de itens vendidos por dia
        int totalVendido = vendasSemanais.Sum(i => i.Quantidade);
        decimal mediaVendasPorDia = (decimal)totalVendido / 30;

        if (mediaVendasPorDia == 0) return "⚡ Estoque estável.";

        // Fórmula preditiva base: Quantidade Atual / Média de Consumo Diário
        decimal diasRestantes = produto.QuantidadeAtual / mediaVendasPorDia;

        if (diasRestantes <= 3)
            return $"⚠️ Crítico: Esgota em aprox. {Math.Round(diasRestantes, 1)} dias! Sugerido comprar lote.";

        return $"✓ Seguro: {Math.Round(diasRestantes, 0)} dias restantes de estoque.";
    }
}