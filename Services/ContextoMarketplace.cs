using Microsoft.EntityFrameworkCore;
using ControlStockBackend.Models;

namespace ControlStockBackend.Data;

public class ContextoMarketplace : DbContext
{
    public ContextoMarketplace(DbContextOptions<ContextoMarketplace> options) : base(options) { }

    public DbSet<Produto> Produtos { get; set; }
    public DbSet<FotoProduto> Fotos { get; set; } // Nova tabela adicionada!
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Venda> Vendas { get; set; }
    public DbSet<ItemVenda> ItensVenda { get; set; }
    public DbSet<MovimentacaoAuditoria> Auditorias { get; set; }
}