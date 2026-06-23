using Microsoft.AspNetCore.Mvc;
using ControlStockBackend.Services;

namespace ControlStockBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IaController : ControllerBase
{
    private readonly IaIntegracaoService _iaService;

    public IaController(IaIntegracaoService iaService)
    {
        _iaService = iaService;
    }

    /// <summary>
    /// Rota que o teu painel administrativo vai chamar.
    /// Exemplo: GET /api/ia/gerar-produto?termo=Camisa+Flamengo+2026
    /// </summary>
    [HttpGet("gerar-produto")]
    public async Task<IActionResult> GerarProduto([FromQuery] string termo)
    {
        if (string.IsNullOrWhiteSpace(termo))
        {
            return BadRequest("O termo de procura não pode estar vazio.");
        }

        // Executa a busca de imagens e geração de textos de forma paralela e ultra rápida
        var tarefaDados = _iaService.GerarDadosProdutoAsync(termo);
        var tarefaImagens = _iaService.BuscarImagensOficiaisAsync(termo);

        await Task.WhenAll(tarefaDados, tarefaImagens);

        var dados = await tarefaDados;
        var imagens = await tarefaImagens;

        if (dados == null)
        {
            return StatusCode(500, "Erro ao processar a geração do produto através da Inteligência Artificial.");
        }

        // Devolve o produto pré-montado com as fotos oficiais prontas para revisão no teu e-commerce
        return Ok(new
        {
            nome = dados.Nome,
            preco = dados.Preco,
            descricao = dados.Descricao,
            categoria = dados.Categoria,
            imagens = imagens
        });
    }
}