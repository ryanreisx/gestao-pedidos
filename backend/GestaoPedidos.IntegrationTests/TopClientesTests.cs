using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GestaoPedidos.Api.Dados;
using GestaoPedidos.Api.Endpoints;

namespace GestaoPedidos.IntegrationTests;

// O banco é compartilhado entre os testes e o relatório não filtra por cliente,
// então cada teste usa um ano que nenhum outro teste usa.
[Collection(ApiCollection.Nome)]
public class TopClientesTests(ApiFixture api)
{
    private static DateTime Utc(int ano, int mes, int dia, int hora = 12, int minuto = 0, int segundo = 0) =>
        new(ano, mes, dia, hora, minuto, segundo, DateTimeKind.Utc);

    [Fact]
    public async Task TopClientes_MaisDeCincoClientesNoPeriodo_RetornaOsCincoDeMaiorValorEmOrdem()
    {
        var produto = await api.CriarProduto(estoque: 1000, preco: 10.00m);
        var clientes = new List<Cliente>();
        for (var n = 1; n <= 7; n++)
        {
            // Cliente n compra n unidades: totais de 10,00 a 70,00.
            var cliente = await api.CriarCliente();
            await api.CriarPedido(cliente.Id, Utc(1981, 6, n), (produto, n));
            clientes.Add(cliente);
        }

        var top = await Top("dataInicio=1981-01-01&dataFim=1981-12-31");

        Assert.Equal(5, top.Count);
        Assert.Equal(
            [clientes[6].Id, clientes[5].Id, clientes[4].Id, clientes[3].Id, clientes[2].Id],
            top.Select(c => c.ClienteId));
        Assert.Equal([70.00m, 60.00m, 50.00m, 40.00m, 30.00m], top.Select(c => c.ValorTotal));
    }

    [Fact]
    public async Task TopClientes_VariosPedidosDoMesmoCliente_SomaValorEContaPedidos()
    {
        var produto = await api.CriarProduto(estoque: 1000, preco: 25.50m);
        var cliente = await api.CriarCliente();
        await api.CriarPedido(cliente.Id, Utc(1982, 2, 1), (produto, 1));
        await api.CriarPedido(cliente.Id, Utc(1982, 5, 1), (produto, 2));
        await api.CriarPedido(cliente.Id, Utc(1982, 9, 1), (produto, 4));

        var top = await Top("dataInicio=1982-01-01&dataFim=1982-12-31");

        var linha = Assert.Single(top);
        Assert.Equal(new TopClienteResponse(cliente.Id, cliente.Nome, 178.50m, 3), linha); // 7 x 25,50
    }

    [Fact]
    public async Task TopClientes_ComPeriodo_ConsideraSoPedidosDosDoisDiasInteiros()
    {
        var produto = await api.CriarProduto(estoque: 1000, preco: 10.00m);
        var dentroEFora = await api.CriarCliente();
        var soFora = await api.CriarCliente();
        await api.CriarPedido(dentroEFora.Id, Utc(1983, 3, 9, 23, 59, 59), (produto, 100)); // véspera
        await api.CriarPedido(dentroEFora.Id, Utc(1983, 3, 10, 0, 0, 0), (produto, 1));     // primeiro instante
        await api.CriarPedido(dentroEFora.Id, Utc(1983, 3, 12, 23, 59, 59), (produto, 2));  // último segundo
        await api.CriarPedido(dentroEFora.Id, Utc(1983, 3, 13, 0, 0, 0), (produto, 100));   // dia seguinte
        await api.CriarPedido(soFora.Id, Utc(1983, 3, 13, 0, 0, 0), (produto, 100));

        var top = await Top("dataInicio=1983-03-10&dataFim=1983-03-12");

        var linha = Assert.Single(top);
        Assert.Equal(new TopClienteResponse(dentroEFora.Id, dentroEFora.Nome, 30.00m, 2), linha);
    }

    [Fact]
    public async Task TopClientes_EmpateNoValor_DesempataPorMaisPedidosEDepoisPorNome()
    {
        var produto = await api.CriarProduto(estoque: 1000, preco: 10.00m);
        var sufixo = Guid.NewGuid().ToString("N");
        var bruno = await api.CriarCliente($"Bruno {sufixo}");
        var carla = await api.CriarCliente($"Carla {sufixo}");
        var ana = await api.CriarCliente($"Ana {sufixo}");
        var diego = await api.CriarCliente($"Diego {sufixo}");
        // Todos somam 60,00. Diego chega lá com 3 pedidos; os outros, com 1.
        await api.CriarPedido(bruno.Id, Utc(1984, 1, 1), (produto, 6));
        await api.CriarPedido(carla.Id, Utc(1984, 1, 2), (produto, 6));
        await api.CriarPedido(ana.Id, Utc(1984, 1, 3), (produto, 6));
        await api.CriarPedido(diego.Id, Utc(1984, 1, 4), (produto, 2));
        await api.CriarPedido(diego.Id, Utc(1984, 1, 5), (produto, 2));
        await api.CriarPedido(diego.Id, Utc(1984, 1, 6), (produto, 2));

        var top = await Top("dataInicio=1984-01-01&dataFim=1984-12-31");

        Assert.Equal([diego.Id, ana.Id, bruno.Id, carla.Id], top.Select(c => c.ClienteId));
        Assert.Equal([3, 1, 1, 1], top.Select(c => c.QuantidadePedidos));
    }

    [Fact]
    public async Task TopClientes_ClientesComOMesmoNome_AparecemSeparados()
    {
        var produto = await api.CriarProduto(estoque: 1000, preco: 10.00m);
        var nome = $"Homônimo {Guid.NewGuid():N}";
        var primeiro = await api.CriarCliente(nome);
        var segundo = await api.CriarCliente(nome);
        await api.CriarPedido(primeiro.Id, Utc(1985, 1, 1), (produto, 5));
        await api.CriarPedido(segundo.Id, Utc(1985, 1, 2), (produto, 3));

        var top = await Top("dataInicio=1985-01-01&dataFim=1985-12-31");

        Assert.Equal(
            [new(primeiro.Id, nome, 50.00m, 1), new(segundo.Id, nome, 30.00m, 1)],
            top);
    }

    [Fact]
    public async Task TopClientes_PeriodoSemPedidos_RetornaListaVazia()
    {
        var top = await Top("dataInicio=1970-01-01&dataFim=1970-12-31");

        Assert.Empty(top);
    }

    [Fact]
    public async Task TopClientes_SoComDataInicio_ConsideraDoDiaEmDiante()
    {
        var produto = await api.CriarProduto(estoque: 1000, preco: 10.00m);
        var cliente = await api.CriarCliente();
        await api.CriarPedido(cliente.Id, Utc(2999, 1, 1), (produto, 4));

        var top = await Top("dataInicio=2999-01-01");

        var linha = Assert.Single(top);
        Assert.Equal(new TopClienteResponse(cliente.Id, cliente.Nome, 40.00m, 1), linha);
    }

    [Fact]
    public async Task TopClientes_SemPeriodo_ConsideraTodosOsPedidos()
    {
        // Valor maior que a soma de qualquer outro teste, para garantir o primeiro lugar.
        var produto = await api.CriarProduto(estoque: 1000, preco: 9_000_000_000.00m);
        var cliente = await api.CriarCliente();
        await api.CriarPedido(cliente.Id, Utc(1986, 1, 1), (produto, 1));

        var top = await Top("");

        Assert.InRange(top.Count, 1, 5);
        Assert.Equal(cliente.Id, top[0].ClienteId);
        Assert.Equal(top.OrderByDescending(c => c.ValorTotal).Select(c => c.ClienteId), top.Select(c => c.ClienteId));
    }

    // ---- 400 ----

    [Fact]
    public async Task TopClientes_DataInicioMaiorQueDataFim_Retorna400ComMensagem()
    {
        var resposta = await api.Client.GetAsync("/relatorios/top-clientes?dataInicio=2026-03-12&dataFim=2026-03-10");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var problema = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("dataInicio não pode ser maior que dataFim.", problema.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("dataInicio=ontem")]
    [InlineData("dataFim=2026-02-30")]
    public async Task TopClientes_DataEmFormatoInvalido_Retorna400EmProblemDetails(string query)
    {
        var resposta = await api.Client.GetAsync($"/relatorios/top-clientes?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }

    // ---- apoio ----

    private async Task<List<TopClienteResponse>> Top(string query)
    {
        var top = await api.Client.GetFromJsonAsync<List<TopClienteResponse>>($"/relatorios/top-clientes?{query}");
        return top!;
    }
}
