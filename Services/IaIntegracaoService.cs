using System.Text.Json;
using System.Text.Json.Serialization;
using ControlStockBackend.Models;

namespace ControlStockBackend.Services;

public class IaIntegracaoService
{
    private readonly HttpClient _httpClient;
    private readonly string _geminiKey;
    private readonly string _googleKey;
    private readonly string _googleCx;

    public IaIntegracaoService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _geminiKey = config["ApiKeys:Gemini"] ?? "";
        _googleKey = config["ApiKeys:GoogleSearch"] ?? "";
        _googleCx = config["ApiKeys:GoogleCx"] ?? "";
    }

    /// <summary>
    /// Procura até 3 URLs oficiais de imagens do produto usando a Google Custom Search API.
    /// </summary>
    public async Task<List<string>> BuscarImagensOficiaisAsync(string termo)
    {
        var urlsImagens = new List<string>();

        // Se as chaves do Google não estiverem configuradas, executa o fallback seguro imediatamente
        if (string.IsNullOrEmpty(_googleKey) || string.IsNullOrEmpty(_googleCx))
        {
            return ObterFallbackImagens(termo);
        }

        try
        {
            var url = $"https://www.googleapis.com/customsearch/v1?q={Uri.EscapeDataString(termo)}&searchType=image&key={_googleKey}&cx={_googleCx}";
            
            var response = await _httpClient.GetFromJsonAsync<GoogleSearchResponse>(url);
            
            if (response?.Items != null && response.Items.Any())
            {
                // Captura as 3 primeiras imagens encontradas para preencher os campos do formulário
                var links = response.Items.Take(3).Select(item => item.Link).ToList();
                urlsImagens.AddRange(links);
            }
        }
        catch (Exception)
        {
            // Fallback seguro em caso de excesso de quota diária gratuita (100 pesquisas) ou erro de rede
            return ObterFallbackImagens(termo);
        }

        // Garante que o retorno tenha sempre pelo menos 3 posições (mesmo que com placeholders)
        while (urlsImagens.Count < 3)
        {
            urlsImagens.Add("https://via.placeholder.com/600x400?text=Sem+Foto+Adicional");
        }

        return urlsImagens;
    }

    /// <summary>
    /// Gera dados comerciais estruturados (Nome, Preço Estimado, Descrição e Categoria) usando o Gemini 1.5 Flash.
    /// </summary>
    public async Task<IaProdutoDadosResult?> GerarDadosProdutoAsync(string termo)
    {
        if (string.IsNullOrEmpty(_geminiKey))
        {
            return null;
        }

        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_geminiKey}";

            var prompt = $@"Gere os dados comerciais de e-commerce para o produto: ""{termo}"". 
            Retorne OBRIGATORIAMENTE apenas um objeto JSON limpo e válido, sem formatação markdown de código, sem aspas triplas ou blocos do tipo ```json. 
            Siga rigorosamente esta estrutura:
            {{
                ""nome"": ""Nome de exibição comercial amigável"",
                ""preco"": 0.00,
                ""descricao"": ""Uma descrição curta, vendedora e atraente para marketing de e-commerce."",
                ""categoria"": ""Categoria mais adequada para este produto""
            }}";

            var payload = new
            {
                contents = new[] {
                    new { parts = new[] { new { text = prompt } } }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(url, payload);
            if (!response.IsSuccessStatusCode) return null;

            var jsonResult = await response.Content.ReadFromJsonAsync<GeminiResponse>();
            var textoJsonRaw = jsonResult?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text?.Trim();

            if (string.IsNullOrEmpty(textoJsonRaw)) return null;

            // Limpa formatações markdown indesejadas (como ```json ou ```) que a IA pode retornar por teimosia
            var jsonLimpo = LimparFormatacaoMarkdown(textoJsonRaw);

            var opcoes = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<IaProdutoDadosResult>(jsonLimpo, opcoes);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private string LimparFormatacaoMarkdown(string input)
    {
        if (input.StartsWith("```"))
        {
            int primeiraQuebra = input.IndexOf('\n');
            int ultimaQuebra = input.LastIndexOf("```");
            if (primeiraQuebra != -1 && ultimaQuebra != -1 && ultimaQuebra > primeiraQuebra)
            {
                return input.Substring(primeiraQuebra + 1, ultimaQuebra - primeiraQuebra - 1).Trim();
            }
        }
        return input.Replace("```json", "").Replace("```", "").Trim();
    }

    private List<string> ObterFallbackImagens(string termo)
    {
        // Links de imagem limpos sem a formatação markdown que quebrava as URLs
        return new List<string>
        {
            "https://images.unsplash.com/photo-1542751371-adc38448a05e?auto=format&fit=crop&w=600&q=80",
            "https://images.unsplash.com/photo-1511512578047-dfb367046420?auto=format&fit=crop&w=600&q=80",
            "https://images.unsplash.com/photo-1550745165-9bc0b252726f?auto=format&fit=crop&w=600&q=80"
        };
    }
}

// =======================================================================
// MODELOS PARA MAPEAMENTO DE APIS EXTERNAS
// =======================================================================
public class IaProdutoDadosResult
{
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
}

public class GoogleSearchResponse
{
    [JsonPropertyName("items")]
    public List<GoogleItem>? Items { get; set; }
}

public class GoogleItem
{
    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;
}

public class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }
}

public class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }
}

public class GeminiContent
{
    [JsonPropertyName("parts")]
    public List<GeminiPart>? Parts { get; set; }
}

public class GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}