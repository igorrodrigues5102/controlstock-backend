using ControlStockBackend.Data;
using ControlStockBackend.Models;
using ControlStockBackend.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =======================================================================
// CONFIGURAÇÃO DOS SERVIÇOS (CONTAINER)
// =======================================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTudo", policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // Libera qualquer origem, incluindo arquivos locais (null)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddScoped<EstoqueInteligenteService>();
builder.Services.AddScoped<AntiFraudeService>();

builder.Services.AddDbContext<ContextoMarketplace>(options =>
    options.UseSqlite("Data Source=marketplace.db"));

var app = builder.Build();

app.UseCors("PermitirTudo");

// =======================================================================
// INICIALIZAÇÃO DO BANCO E SEED DATA
// =======================================================================
using (var escopo = app.Services.CreateScope())
{
    var db = escopo.ServiceProvider.GetRequiredService<ContextoMarketplace>();
    db.Database.Migrate();

    if (!db.Produtos.Any())
    {
        db.Produtos.AddRange(
            new Produto
            {
                Nome = "Notebook Gamer Pro",
                Preco = 4599.90m,
                QuantidadeAtual = 8,
                EstoqueMinimo = 2,
                Descricao = "Intel i7, 16GB RAM, RTX 3060.",
                Fotos = new List<FotoProduto>
                {
                    new FotoProduto { Url = "https://images.unsplash.com/photo-1603302576837-37561b2e2302?w=500" },
                    new FotoProduto { Url = "https://images.unsplash.com/photo-1588872657578-7efd1f1555ed?w=500" }
                }
            },
            new Produto
            {
                Nome = "Smart TV 4K 55\"",
                Preco = 2899.00m,
                QuantidadeAtual = 5,
                EstoqueMinimo = 2,
                Descricao = "Tela QLED, HDR10+.",
                Fotos = new List<FotoProduto>
                {
                    new FotoProduto { Url = "https://images.unsplash.com/photo-1593305841991-05c297ba4575?w=500" },
                    new FotoProduto { Url = "https://images.unsplash.com/photo-1552975084-6e027cd345c2?w=500" }
                }
            }
        );
        db.SaveChanges();
    }

    if (!db.Usuarios.Any())
    {
        db.Usuarios.Add(new Usuario { Nome = "Administrador", Email = "admin", Senha = "123", Nivel = "ADMIN" });
        db.SaveChanges();
    }

    if (!db.Vendas.Any())
    {
        var vendaFake = new Venda
        {
            NomeCliente = "Igor Teste IA",
            DocumentoCliente = "PERFIL AUTENTICADO",
            FormaPagamento = "PIX",
            DataVenda = DateTime.Now.AddDays(-5)
        };
        db.Vendas.Add(vendaFake);
        db.SaveChanges();

        db.ItensVenda.AddRange(
            new ItemVenda { VendaId = vendaFake.Id, ProdutoId = 1, Quantidade = 15, PrecoUnitario = 4599.90m },
            new ItemVenda { VendaId = vendaFake.Id, ProdutoId = 2, Quantidade = 5, PrecoUnitario = 2899.00m }
        );

        db.Auditorias.Add(new MovimentacaoAuditoria
        {
            ProdutoId = 1,
            Tipo = "SAIDA",
            Quantidade = 15,
            Observacao = "Venda histórica simulada para IA."
        });
        db.SaveChanges();
    }
}

// =======================================================================
// ENDPOINTS DA API REST
// =======================================================================

// 📊 ROTA METRICAS DO TOPO DO PAINEL
app.MapGet("/api/admin/dashboard/topo", async (ContextoMarketplace db) =>
{
    try
    {
        var patrimonio = await db.Produtos.SumAsync(p => (decimal?)p.QuantidadeAtual * p.Preco) ?? 0;
        var pedidos = await db.Vendas.CountAsync();
        var saidas = await db.ItensVenda.SumAsync(i => (int?)i.Quantidade) ?? 0;

        return Results.Ok(new
        {
            patrimonioEstoque = patrimonio,
            pedidosFaturados = pedidos,
            totalItensSaidos = saidas
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { erro = ex.Message });
    }
});

// 1. Listar Catálogo de Produtos
app.MapGet("/api/produtos", async (ContextoMarketplace db) =>
    Results.Ok(await db.Produtos.Include(p => p.Fotos).ToListAsync()));

// 2. Cadastrar Novo Produto via Site
app.MapPost("/api/produtos", async (ContextoMarketplace db, Produto novoProd) =>
{
    db.Produtos.Add(novoProd);
    await db.SaveChangesAsync();

    db.Auditorias.Add(new MovimentacaoAuditoria
    {
        ProdutoId = novoProd.Id,
        Tipo = "ENTRADA",
        Quantidade = novoProd.QuantidadeAtual,
        Observacao = $"Carga inicial via Web Admin por {novoProd.Nome}."
    });
    await db.SaveChangesAsync();

    return Results.Ok(new { mensagem = $"🎉 Produto '{novoProd.Nome}' cadastrado com sucesso no SQLite!" });
});

// 3. Autenticação Híbrida (Login)
app.MapPost("/api/login", async (ContextoMarketplace db, LoginRequest req) =>
{
    var user = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == req.Email && u.Senha == req.Senha);
    if (user == null) return Results.Json(new { autenticado = false }, statusCode: 401);

    return Results.Ok(new { autenticado = true, nome = user.Nome, email = user.Email, nivel = user.Nivel, mensagem = $"👋 Bem-vindo de volta, {user.Nome}!" });
});

// 4. Registro de Clientes
app.MapPost("/api/registrar", async (ContextoMarketplace db, Usuario novoUser) =>
{
    var existe = await db.Usuarios.AnyAsync(u => u.Email == novoUser.Email);
    if (existe) return Results.BadRequest(new { mensagem = "Este e-mail ou usuário já está em uso." });

    novoUser.Nivel = "CLIENTE";
    db.Usuarios.Add(novoUser);
    await db.SaveChangesAsync();

    return Results.Ok(new { nome = novoUser.Nome, email = novoUser.Email, nivel = novoUser.Nivel, message = "Cadastro realizado com sucesso!" });
});

// 5. Finalizar Venda e Abater Estoque
app.MapPost("/api/vendas", async (ContextoMarketplace db, PedidoRequest pedido, AntiFraudeService antiFraude) =>
{
    decimal valorTotalProvisorio = 0;
    foreach (var item in pedido.Itens)
    {
        var produtoRef = await db.Produtos.FindAsync(item.Id);
        if (produtoRef != null) valorTotalProvisorio += (produtoRef.Preco * item.Qtd);
    }

    int riscoScore = antiFraude.AnalisarRiscoVenda(pedido, valorTotalProvisorio);
    if (riscoScore >= 80)
    {
        return Results.BadRequest(new { mensagem = "❌ Transação bloqueada temporariamente pela IA: Comportamento de compra com risco de fraude crítico." });
    }

    using var transacao = await db.Database.BeginTransactionAsync();
    try
    {
        decimal totalVenda = 0;
        var novaVenda = new Venda { NomeCliente = pedido.NomeCliente, DocumentoCliente = pedido.DocumentoCliente, FormaPagamento = pedido.FormaPagamento, DataVenda = DateTime.Now };
        db.Vendas.Add(novaVenda);
        await db.SaveChangesAsync();

        foreach (var item in pedido.Itens)
        {
            var produto = await db.Produtos.FindAsync(item.Id);
            if (produto == null || produto.QuantidadeAtual < item.Qtd)
                return Results.BadRequest(new { margin = $"Estoque insuficiente ou produto inválido: {item.Nome}" });

            produto.QuantidadeAtual -= item.Qtd;

            var detalhe = new ItemVenda { VendaId = novaVenda.Id, ProdutoId = produto.Id, Quantidade = item.Qtd, PrecoUnitario = produto.Preco };
            db.ItensVenda.Add(detalhe);

            totalVenda += (produto.Preco * item.Qtd);

            db.Auditorias.Add(new MovimentacaoAuditoria
            {
                ProdutoId = produto.Id,
                Tipo = "SAIDA",
                Quantidade = item.Qtd,
                Observacao = $"Venda #{novaVenda.Id} consumida por {pedido.NomeCliente}."
            });
        }

        await db.SaveChangesAsync();
        await transacao.CommitAsync();

        return Results.Ok(new { mensagem = "Venda processada com sucesso!", vendaId = novaVenda.Id });
    }
    catch
    {
        await transacao.RollbackAsync();
        return Results.StatusCode(500);
    }
});

// 6. Dados Consolidados do Dashboard Admin
app.MapGet("/api/admin/dashboard", async (ContextoMarketplace db) =>
{
    var productsList = await db.Produtos.ToListAsync();
    decimal patrimonioEstoque = productsList.Sum(p => p.Preco * p.QuantidadeAtual);

    var vendas = await db.Vendas.ToListAsync();
    int totalVendas = vendas.Count;

    var itensVendidos = await db.ItensVenda.SumAsync(i => i.Quantidade);
    var auditoria = await db.Auditorias.OrderByDescending(a => a.Id).Take(15).ToListAsync();

    var itensVendaAgrupados = await db.ItensVenda.Include(i => i.Produto).ToListAsync();
    var dadosGrafico = itensVendaAgrupados
        .GroupBy(i => i.Produto!.Nome)
        .Select(g => new { produto = g.Key, faturamento = g.Sum(x => x.Quantidade * x.PrecoUnitario) })
        .ToList();

    return Results.Ok(new { patrimonioEstoque, vendasRealizadas = totalVendas, itensVendidos, auditoria, grafico = dadosGrafico });
});

// 7. Rota para as previsões da IA de estoque
app.MapGet("/api/admin/previsoes", async (ContextoMarketplace db, EstoqueInteligenteService iaEstoque) =>
{
    var produtos = await db.Produtos.ToListAsync();
    var relatorioInteligente = new List<object>();

    foreach (var prod in produtos)
    {
        string previsaoIA = await iaEstoque.PreverDiasRestantes(prod.Id);
        relatorioInteligente.Add(new
        {
            prod.Id,
            prod.Nome,
            prod.QuantidadeAtual,
            PrevisaoEsgotamento = previsaoIA
        });
    }

    return Results.Ok(relatorioInteligente);
});

// 8. Entrada de Estoque por Lote
app.MapPost("/api/admin/estoque/lote", async (ContextoMarketplace db, LoteRequest req) =>
{
    var produto = await db.Produtos.FindAsync(req.ProdutoId);
    if (produto == null) return Results.NotFound(new { mensagem = "❌ Produto não encontrado no banco." });

    produto.QuantidadeAtual += req.Quantidade;

    db.Auditorias.Add(new MovimentacaoAuditoria
    {
        ProdutoId = produto.Id,
        Tipo = "ENTRADA",
        Quantidade = req.Quantidade,
        Observacao = $"Lote adicionado via Painel: {req.Observacao}"
    });

    await db.SaveChangesAsync();
    return Results.Ok(new { mensagem = $"✓ Lote de {req.Quantidade} un. para '{produto.Nome}' registrado com sucesso!" });
});

// 9. ROTA CORRIGIDA: GESTÃO AVANÇADA (NOME, PREÇO COM %, FOTOS)
app.MapPut("/api/admin/produtos/precos", async (ContextoMarketplace db, PrecoAvancadoRequest req) =>
{
    var produto = await db.Produtos.Include(p => p.Fotos).FirstOrDefaultAsync(p => p.Id == req.produtoId);
    if (produto == null) return Results.NotFound(new { mensagem = "❌ Produto não encontrado." });

    // 1. Atualização do Nome
    if (!string.IsNullOrEmpty(req.novoNome))
    {
        produto.Nome = req.novoNome;
    }

    // 2. Cálculo do Preço com Desconto baseado em Porcentagem (%) usando a propriedade real 'Preco'
    decimal precoBaseCalculo = req.novoPrecoBase > 0 ? req.novoPrecoBase : produto.Preco;
    if (req.porcentagemDesconto > 0 && req.porcentagemDesconto <= 100)
    {
        decimal fatorDesconto = (100 - req.porcentagemDesconto) / 100m;
        produto.Preco = precoBaseCalculo * fatorDesconto;
    }
    else
    {
        produto.Preco = precoBaseCalculo;
    }

    // 3. Atualização das Fotos
    var urlsRecebidas = new List<string> { req.url1, req.url2, req.url3 }.Where(u => !string.IsNullOrEmpty(u)).ToList();
    if (urlsRecebidas.Any())
    {
        db.Fotos.RemoveRange(produto.Fotos);

        foreach (var url in urlsRecebidas)
        {
            db.Fotos.Add(new FotoProduto { Url = url, ProdutoId = produto.Id });
        }
    }

    db.Auditorias.Add(new MovimentacaoAuditoria
    {
        ProdutoId = produto.Id,
        Tipo = "ALTERACAO",
        Quantidade = 0,
        Observacao = $"Edição completa via Web. Preço final: R$ {produto.Preco.ToString("F2")} (Desconto de {req.porcentagemDesconto}%)."
    });

    await db.SaveChangesAsync();
    return Results.Ok(new { mensagem = $"✓ Produto '{produto.Nome}' atualizado com sucesso no SQLite!" });
});

// 9.B ROTA COMPLEMENTAR: EXCLUIR PRODUTO DO BANCO
app.MapDelete("/api/admin/produtos/excluir/{id}", async (ContextoMarketplace db, int id) =>
{
    var produto = await db.Produtos.Include(p => p.Fotos).FirstOrDefaultAsync(p => p.Id == id);
    if (produto == null) return Results.NotFound(new { mensagem = "❌ Produto não localizado." });

    db.Fotos.RemoveRange(produto.Fotos);
    db.Produtos.Remove(produto);

    await db.SaveChangesAsync();
    return Results.Ok(new { mensagem = $"✓ Produto ID {id} foi removido com sucesso do SQLite!" });
});

// 🔌 ROTA DE PROCESSAR VENDA (FECHAR PEDIDO)
app.MapPost("/api/pedidos/fechar", async (ContextoMarketplace db, PedidoRequest req) =>
{
    int totalItens = req.Itens.Sum(i => i.Qtd);
    if (totalItens > 50)
    {
        return Results.BadRequest(new { mensagem = "❌ Transação bloqueada temporariamente pela IA: Comportamento de compra com risco de fraude crítico." });
    }

    using var transacao = await db.Database.BeginTransactionAsync();
    try
    {
        decimal totalFaturado = 0;
        var novaVenda = new Venda { NomeCliente = req.NomeCliente, DocumentoCliente = req.DocumentoCliente, FormaPagamento = req.FormaPagamento, DataVenda = DateTime.Now };
        db.Vendas.Add(novaVenda);
        await db.SaveChangesAsync();

        foreach (var item in req.Itens)
        {
            var produto = await db.Produtos.FindAsync(item.Id);
            if (produto == null || produto.QuantidadeAtual < item.Qtd)
            {
                await transacao.RollbackAsync();
                return Results.BadRequest(new { mensagem = $"Estoque insuficiente para o produto ID {item.Id}." });
            }

            produto.QuantidadeAtual -= item.Qtd;
            totalFaturado += produto.Preco * item.Qtd;

            db.ItensVenda.Add(new ItemVenda { VendaId = novaVenda.Id, ProdutoId = produto.Id, Quantidade = item.Qtd, PrecoUnitario = produto.Preco });

            db.Auditorias.Add(new MovimentacaoAuditoria
            {
                ProdutoId = produto.Id,
                Tipo = "SAIDA",
                Quantidade = item.Qtd,
                Observacao = $"Venda realizada para {req.NomeCliente} via {req.FormaPagamento}."
            });
        }

        await db.SaveChangesAsync();
        await transacao.CommitAsync();

        return Results.Ok(new { mensagem = "🎉 Compra finalizada! Estoque atualizado e dados repassados ao painel administrativo." });
    }
    catch (Exception)
    {
        await transacao.RollbackAsync();
        return Results.StatusCode(500);
    }
});

// 🔌 ROTA DE EXPORTAR PLANILHA EXCEL/CSV
app.MapGet("/api/admin/relatorios/exportar", async (ContextoMarketplace db) =>
{
    var auditorias = await db.Auditorias.OrderByDescending(a => a.Id).ToListAsync();
    var produtos = await db.Produtos.ToDictionaryAsync(p => p.Id, p => p.Nome);

    var csv = new System.Text.StringBuilder();
    csv.AppendLine("ID_Auditoria;ID_Produto;Nome_Produto;Movimentacao;Quantidade;Observacao");

    foreach (var item in auditorias)
    {
        produtos.TryGetValue(item.ProdutoId, out var nomeProduto);
        nomeProduto ??= "Produto Oculto/Removido";
        csv.AppendLine($"{item.Id};{item.ProdutoId};{nomeProduto};{item.Tipo};{item.Quantidade};{item.Observacao}");
    }

    var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
    return Results.File(bytes, "text/csv", "Relatorio_Estoque_ControlStock.csv");
});

// 10. CRIAR PRODUTO NOVO PELO PAINEL
app.MapPost("/api/admin/produtos/novo", async (ContextoMarketplace db, CadastroProdutoRequest req) =>
{
    var novoProduto = new Produto
    {
        Nome = req.nome,
        Preco = req.preco,
        QuantidadeAtual = req.estoqueInicial,
        EstoqueMinimo = 2,
        Descricao = "Cadastrado via Painel Administrativo."
    };

    db.Produtos.Add(novoProduto);
    await db.SaveChangesAsync();

    var urlsFotos = new List<string> { req.url1, req.url2, req.url3 };
    foreach (var url in urlsFotos.Where(u => !string.IsNullOrEmpty(u)))
    {
        db.Fotos.Add(new FotoProduto { Url = url, ProdutoId = novoProduto.Id });
    }

    db.Auditorias.Add(new MovimentacaoAuditoria
    {
        ProdutoId = novoProduto.Id,
        Tipo = "CADASTRO",
        Quantidade = req.estoqueInicial,
        Observacao = $"Cadastro inicial de produto com carga de {req.estoqueInicial} un."
    });

    await db.SaveChangesAsync();
    return Results.Ok(new { mensagem = $"🎉 Produto '{req.nome}' e suas fotos foram salvos no SQLite com sucesso!" });
});

// 🔍 ROTA DE FALLBACK SEGURO DE IMAGENS
app.MapGet("/api/admin/produtos/buscar-imagens", (string termo) =>
{
    if (string.IsNullOrEmpty(termo)) return Results.BadRequest("Termo de busca vazio.");

    string termoFormatado = Uri.EscapeDataString(termo.ToLower().Replace(" ", ","));

    string url1 = $"https://images.unsplash.com/photo-1542751371-adc38448a05e?auto=format&fit=crop&w=600&q=80&q={termoFormatado}";
    string url2 = $"https://images.unsplash.com/photo-1511512578047-dfb367046420?auto=format&fit=crop&w=600&q=80&q={termoFormatado}";
    string url3 = $"https://images.unsplash.com/photo-1550745165-9bc0b252726f?auto=format&fit=crop&w=600&q=80&q={termoFormatado}";

    if (termo.ToLower().Contains("chuteira") || termo.ToLower().Contains("umbro") || termo.ToLower().Contains("camisa") || termo.ToLower().Contains("seleção"))
    {
        url1 = "https://images.unsplash.com/photo-1511886929837-354d827aae26?auto=format&fit=crop&w=600&q=80";
        url2 = "https://images.unsplash.com/photo-1508098682722-e99c43a406b2?auto=format&fit=crop&w=600&q=80";
        url3 = "https://images.unsplash.com/photo-1543351611-58f69d7c1781?auto=format&fit=crop&w=600&q=80";
    }

    return Results.Ok(new { url1, url2, url3 });
});

app.Run();

// =======================================================================
// MODELOS DE ENTRADA DE DADOS (ESTRUTURAS DTO)
// =======================================================================
public record PrecoAvancadoRequest(int produtoId, string novoNome, decimal novoPrecoBase, int porcentagemDesconto, string url1, string url2, string url3);