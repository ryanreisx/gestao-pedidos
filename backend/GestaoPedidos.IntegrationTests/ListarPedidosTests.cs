using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GestaoPedidos.Api.Endpoints;

namespace GestaoPedidos.IntegrationTests;

// O banco é compartilhado entre os testes, então cada teste filtra pelo cliente que criou
// (ou por um período que só ele usa) para enxergar apenas os próprios pedidos.
[Collection(ApiCollection.Nome)]
public class ListarPedidosTests(ApiFixture api)
{
    private static DateTime Utc(int ano, int mes, int dia, int hora = 12, int minuto = 0, int segundo = 0) =>
        new(ano, mes, dia, hora, minuto, segundo, DateTimeKind.Utc);

    // ---- formato e ordenação ----

    [Fact]
    public async Task ListarPedidos_FiltroPorCliente_RetornaSoOsPedidosDoClienteDoMaisRecenteParaOMaisAntigo()
    {
        var cliente = await api.CriarCliente();
        var outroCliente = await api.CriarCliente();
        var produto = await api.CriarProduto(estoque: 100);
        var antigo = await api.CriarPedido(cliente.Id, Utc(2026, 1, 5), (produto, 1));
        var recente = await api.CriarPedido(cliente.Id, Utc(2026, 6, 20), (produto, 1));
        var doMeio = await api.CriarPedido(cliente.Id, Utc(2026, 3, 10), (produto, 1));
        await api.CriarPedido(outroCliente.Id, Utc(2026, 4, 1), (produto, 1));

        var pagina = await Listar($"clienteId={cliente.Id}");

        Assert.Equal(3, pagina.Total);
        Assert.Equal([recente.Id, doMeio.Id, antigo.Id], pagina.Itens.Select(p => p.Id));
        Assert.All(pagina.Itens, p => Assert.Equal(cliente.Id, p.ClienteId));
    }

    [Fact]
    public async Task ListarPedidos_PedidoComItens_RetornaNoFormatoDaSpec()
    {
        var cliente = await api.CriarCliente();
        var teclado = await api.CriarProduto(estoque: 100, preco: 50.00m);
        var mouse = await api.CriarProduto(estoque: 100, preco: 19.90m);
        var criado = await api.CriarPedido(cliente.Id, Utc(2026, 11, 15, 14, 30), (teclado, 3), (mouse, 2));

        var resposta = await api.Client.GetAsync($"/pedidos?clienteId={cliente.Id}");
        var json = await resposta.Content.ReadAsStringAsync();
        var pagina = JsonSerializer.Deserialize<PaginaDePedidosResponse>(json, JsonSerializerOptions.Web)!;

        var pedido = Assert.Single(pagina.Itens);
        Assert.Equal(criado.Id, pedido.Id);
        Assert.Equal(cliente.Nome, pedido.ClienteNome);
        Assert.Equal(Utc(2026, 11, 15, 14, 30), pedido.DataPedido);
        Assert.Equal(189.80m, pedido.ValorTotal); // 3 x 50,00 + 2 x 19,90
        Assert.Equal(
            [new(teclado.Id, teclado.Nome, 3, 50.00m), new(mouse.Id, mouse.Nome, 2, 19.90m)],
            pedido.Itens);

        // A data sai em UTC, com o "Z".
        var dataNoJson = JsonDocument.Parse(json).RootElement
            .GetProperty("itens")[0].GetProperty("dataPedido").GetString();
        Assert.Equal("2026-11-15T14:30:00Z", dataNoJson);
    }

    [Fact]
    public async Task ListarPedidos_PedidoCriadoPeloPost_ApareceNaListagem()
    {
        var cliente = await api.CriarCliente();
        var produto = await api.CriarProduto(estoque: 10);
        var post = await api.Client.PostAsJsonAsync("/pedidos", new
        {
            clienteId = cliente.Id,
            itens = new[] { new { produtoId = produto.Id, quantidade = 2 } }
        });
        var criado = await post.Content.ReadFromJsonAsync<PedidoResponse>();

        var pagina = await Listar($"clienteId={cliente.Id}");

        var listado = Assert.Single(pagina.Itens);
        Assert.Equal(criado!.Id, listado.Id);
        Assert.Equal(criado.ValorTotal, listado.ValorTotal);
    }

    [Fact]
    public async Task ListarPedidos_ClienteSemPedidos_RetornaPaginaVazia()
    {
        var cliente = await api.CriarCliente();

        var pagina = await Listar($"clienteId={cliente.Id}");

        Assert.Empty(pagina.Itens);
        Assert.Equal(0, pagina.Total);
    }

    // ---- período ----

    [Fact]
    public async Task ListarPedidos_FiltroPorPeriodo_IncluiOsDoisDiasInteiros()
    {
        var cliente = await api.CriarCliente();
        var produto = await api.CriarProduto(estoque: 100);
        await api.CriarPedido(cliente.Id, Utc(2026, 3, 9, 23, 59, 59), (produto, 1));                  // véspera
        var primeiroInstante = await api.CriarPedido(cliente.Id, Utc(2026, 3, 10, 0, 0, 0), (produto, 1));
        var ultimoInstante = await api.CriarPedido(cliente.Id, Utc(2026, 3, 12, 23, 59, 59), (produto, 1));
        await api.CriarPedido(cliente.Id, Utc(2026, 3, 13, 0, 0, 0), (produto, 1));                    // dia seguinte

        var pagina = await Listar($"clienteId={cliente.Id}&dataInicio=2026-03-10&dataFim=2026-03-12");

        Assert.Equal(2, pagina.Total);
        Assert.Equal([ultimoInstante.Id, primeiroInstante.Id], pagina.Itens.Select(p => p.Id));
    }

    [Fact]
    public async Task ListarPedidos_SoComDataInicio_RetornaDoDiaEmDiante()
    {
        var cliente = await api.CriarCliente();
        var produto = await api.CriarProduto(estoque: 100);
        await api.CriarPedido(cliente.Id, Utc(2026, 3, 9), (produto, 1));
        var noDia = await api.CriarPedido(cliente.Id, Utc(2026, 3, 10), (produto, 1));
        var depois = await api.CriarPedido(cliente.Id, Utc(2026, 8, 1), (produto, 1));

        var pagina = await Listar($"clienteId={cliente.Id}&dataInicio=2026-03-10");

        Assert.Equal([depois.Id, noDia.Id], pagina.Itens.Select(p => p.Id));
    }

    [Fact]
    public async Task ListarPedidos_SoComDataFim_RetornaAteODia()
    {
        var cliente = await api.CriarCliente();
        var produto = await api.CriarProduto(estoque: 100);
        var antes = await api.CriarPedido(cliente.Id, Utc(2025, 12, 1), (produto, 1));
        var noDia = await api.CriarPedido(cliente.Id, Utc(2026, 3, 10, 23, 0), (produto, 1));
        await api.CriarPedido(cliente.Id, Utc(2026, 3, 11), (produto, 1));

        var pagina = await Listar($"clienteId={cliente.Id}&dataFim=2026-03-10");

        Assert.Equal([noDia.Id, antes.Id], pagina.Itens.Select(p => p.Id));
    }

    [Fact]
    public async Task ListarPedidos_SoComPeriodo_RetornaPedidosDeTodosOsClientes()
    {
        // Nenhum outro teste usa o ano de 1999.
        var clienteA = await api.CriarCliente();
        var clienteB = await api.CriarCliente();
        var produto = await api.CriarProduto(estoque: 100);
        var pedidoA = await api.CriarPedido(clienteA.Id, Utc(1999, 5, 1), (produto, 1));
        var pedidoB = await api.CriarPedido(clienteB.Id, Utc(1999, 5, 2), (produto, 1));

        var pagina = await Listar("dataInicio=1999-01-01&dataFim=1999-12-31");

        Assert.Equal(2, pagina.Total);
        Assert.Equal([pedidoB.Id, pedidoA.Id], pagina.Itens.Select(p => p.Id));
    }

    // ---- paginação ----

    [Fact]
    public async Task ListarPedidos_SemParametrosDePaginacao_UsaPagina1ComDezItens()
    {
        var cliente = await api.CriarCliente();
        var produto = await api.CriarProduto(estoque: 100);
        for (var dia = 1; dia <= 12; dia++)
            await api.CriarPedido(cliente.Id, Utc(2026, 5, dia), (produto, 1));

        var pagina = await Listar($"clienteId={cliente.Id}");

        Assert.Equal(1, pagina.Page);
        Assert.Equal(10, pagina.PageSize);
        Assert.Equal(12, pagina.Total);
        Assert.Equal(10, pagina.Itens.Count);
    }

    [Fact]
    public async Task ListarPedidos_ComPaginacao_RetornaCadaPaginaSemRepetirPedidos()
    {
        var cliente = await api.CriarCliente();
        var produto = await api.CriarProduto(estoque: 100);
        var ids = new List<int>();
        for (var dia = 1; dia <= 5; dia++)
            ids.Add((await api.CriarPedido(cliente.Id, Utc(2026, 5, dia), (produto, 1))).Id);
        ids.Reverse(); // do mais recente para o mais antigo

        var pagina1 = await Listar($"clienteId={cliente.Id}&page=1&pageSize=2");
        var pagina2 = await Listar($"clienteId={cliente.Id}&page=2&pageSize=2");
        var pagina3 = await Listar($"clienteId={cliente.Id}&page=3&pageSize=2");
        var pagina4 = await Listar($"clienteId={cliente.Id}&page=4&pageSize=2");

        Assert.Equal(ids[..2], pagina1.Itens.Select(p => p.Id));
        Assert.Equal(ids[2..4], pagina2.Itens.Select(p => p.Id));
        Assert.Equal(ids[4..], pagina3.Itens.Select(p => p.Id));
        Assert.Empty(pagina4.Itens);
        Assert.All([pagina1, pagina2, pagina3, pagina4], p => Assert.Equal(5, p.Total));
        Assert.Equal((2, 2), (pagina2.Page, pagina2.PageSize));
    }

    [Fact]
    public async Task ListarPedidos_PageSizeCinquenta_EhAceito()
    {
        var resposta = await api.Client.GetAsync("/pedidos?pageSize=50");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    // ---- 400 ----

    [Theory]
    [InlineData("page=0", "page deve ser maior ou igual a 1.")]
    [InlineData("page=-1", "page deve ser maior ou igual a 1.")]
    [InlineData("pageSize=0", "pageSize deve estar entre 1 e 50.")]
    [InlineData("pageSize=51", "pageSize deve estar entre 1 e 50.")]
    [InlineData("dataInicio=2026-03-12&dataFim=2026-03-10", "dataInicio não pode ser maior que dataFim.")]
    public async Task ListarPedidos_ParametroForaDaFaixa_Retorna400ComMensagem(string query, string mensagem)
    {
        var resposta = await api.Client.GetAsync($"/pedidos?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var problema = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(mensagem, problema.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("page=abc")]
    [InlineData("pageSize=dez")]
    [InlineData("clienteId=x")]
    [InlineData("dataInicio=ontem")]
    [InlineData("dataFim=2026-02-30")]
    public async Task ListarPedidos_ParametroEmFormatoInvalido_Retorna400EmProblemDetails(string query)
    {
        var resposta = await api.Client.GetAsync($"/pedidos?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }

    // ---- apoio ----

    private async Task<PaginaDePedidosResponse> Listar(string query)
    {
        var pagina = await api.Client.GetFromJsonAsync<PaginaDePedidosResponse>($"/pedidos?{query}");
        return pagina!;
    }
}
