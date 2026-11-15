using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GestaoPedidos.Api.Dados;
using GestaoPedidos.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

namespace GestaoPedidos.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class CriarPedidoTests(ApiFixture api)
{
    // ---- 201 ----

    [Fact]
    public async Task CriarPedido_Valido_Retorna201ComPedidoNoFormatoDaSpec()
    {
        var cliente = await CriarCliente();
        var teclado = await CriarProduto(preco: 50.00m, estoque: 10);
        var mouse = await CriarProduto(preco: 19.90m, estoque: 10);
        var antes = DateTime.UtcNow;

        var resposta = await Post(new
        {
            clienteId = cliente.Id,
            itens = new[]
            {
                new { produtoId = teclado.Id, quantidade = 3 },
                new { produtoId = mouse.Id, quantidade = 2 }
            }
        });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var json = await resposta.Content.ReadAsStringAsync();
        var pedido = JsonSerializer.Deserialize<PedidoResponse>(json, JsonSerializerOptions.Web)!;

        Assert.True(pedido.Id > 0);
        Assert.Equal(cliente.Id, pedido.ClienteId);
        Assert.Equal(cliente.Nome, pedido.ClienteNome);
        Assert.InRange(pedido.DataPedido, antes.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(189.80m, pedido.ValorTotal); // 3 x 50,00 + 2 x 19,90
        Assert.Equal(2, pedido.Itens.Count);
        Assert.Equal(new ItemPedidoResponse(teclado.Id, teclado.Nome, 3, 50.00m), pedido.Itens[0]);
        Assert.Equal(new ItemPedidoResponse(mouse.Id, mouse.Nome, 2, 19.90m), pedido.Itens[1]);

        // A data sai em UTC, com o "Z".
        var dataNoJson = JsonDocument.Parse(json).RootElement.GetProperty("dataPedido").GetString();
        Assert.EndsWith("Z", dataNoJson);
    }

    [Fact]
    public async Task CriarPedido_Valido_GravaPedidoEBaixaEstoque()
    {
        var cliente = await CriarCliente();
        var teclado = await CriarProduto(preco: 50.00m, estoque: 10);
        var mouse = await CriarProduto(preco: 19.90m, estoque: 5);

        var resposta = await Post(new
        {
            clienteId = cliente.Id,
            itens = new[]
            {
                new { produtoId = teclado.Id, quantidade = 3 },
                new { produtoId = mouse.Id, quantidade = 5 }
            }
        });
        var criado = await resposta.Content.ReadFromJsonAsync<PedidoResponse>();

        await using var db = api.CriarDbContext();
        Assert.Equal(7, await Estoque(teclado.Id));
        Assert.Equal(0, await Estoque(mouse.Id));

        var gravado = await db.Pedidos.Include(p => p.Itens).SingleAsync(p => p.Id == criado!.Id);
        Assert.Equal(cliente.Id, gravado.ClienteId);
        Assert.Equal(249.50m, gravado.ValorTotal); // 3 x 50,00 + 5 x 19,90
        Assert.Equal(gravado.ValorTotal, gravado.Itens.Sum(i => i.Quantidade * i.PrecoUnitario));
        Assert.Equal(2, gravado.Itens.Count);
    }

    // ---- 400 ----

    [Fact]
    public async Task CriarPedido_SemItens_Retorna400()
    {
        var cliente = await CriarCliente();

        var resposta = await Post(new { clienteId = cliente.Id, itens = Array.Empty<object>() });

        await AssertProblema(resposta, HttpStatusCode.BadRequest, "O pedido precisa ter pelo menos 1 item.");
    }

    [Fact]
    public async Task CriarPedido_SemCampoItens_Retorna400()
    {
        var cliente = await CriarCliente();

        var resposta = await Post(new { clienteId = cliente.Id });

        await AssertProblema(resposta, HttpStatusCode.BadRequest, "O pedido precisa ter pelo menos 1 item.");
    }

    [Fact]
    public async Task CriarPedido_SemClienteId_Retorna400()
    {
        var produto = await CriarProduto(estoque: 10);

        var resposta = await Post(new { itens = new[] { new { produtoId = produto.Id, quantidade = 1 } } });

        await AssertProblema(resposta, HttpStatusCode.BadRequest, "clienteId é obrigatório.");
    }

    [Fact]
    public async Task CriarPedido_QuantidadeZero_Retorna400ENaoAlteraEstoque()
    {
        var cliente = await CriarCliente();
        var produto = await CriarProduto(estoque: 10);

        var resposta = await Post(new
        {
            clienteId = cliente.Id,
            itens = new[] { new { produtoId = produto.Id, quantidade = 0 } }
        });

        await AssertProblema(resposta, HttpStatusCode.BadRequest,
            $"A quantidade do produto {produto.Id} deve ser maior que zero.");
        Assert.Equal(10, await Estoque(produto.Id));
    }

    [Fact]
    public async Task CriarPedido_ProdutoRepetido_Retorna400ENaoAlteraEstoque()
    {
        var cliente = await CriarCliente();
        var produto = await CriarProduto(estoque: 10);

        var resposta = await Post(new
        {
            clienteId = cliente.Id,
            itens = new[]
            {
                new { produtoId = produto.Id, quantidade = 1 },
                new { produtoId = produto.Id, quantidade = 2 }
            }
        });

        await AssertProblema(resposta, HttpStatusCode.BadRequest,
            $"O produto {produto.Id} aparece em mais de um item.");
        Assert.Equal(10, await Estoque(produto.Id));
    }

    [Fact]
    public async Task CriarPedido_JsonInvalido_Retorna400EmProblemDetails()
    {
        var resposta = await api.Client.PostAsync("/pedidos",
            new StringContent("{ isso não é json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }

    // ---- 422 ----

    [Fact]
    public async Task CriarPedido_ClienteInexistente_Retorna422ENaoAlteraEstoque()
    {
        var produto = await CriarProduto(estoque: 10);

        var resposta = await Post(new
        {
            clienteId = int.MaxValue,
            itens = new[] { new { produtoId = produto.Id, quantidade = 1 } }
        });

        await AssertProblema(resposta, HttpStatusCode.UnprocessableEntity, $"Cliente {int.MaxValue} não existe.");
        Assert.Equal(10, await Estoque(produto.Id));
    }

    [Fact]
    public async Task CriarPedido_ProdutoInexistente_Retorna422()
    {
        var cliente = await CriarCliente();

        var resposta = await Post(new
        {
            clienteId = cliente.Id,
            itens = new[] { new { produtoId = int.MaxValue, quantidade = 1 } }
        });

        await AssertProblema(resposta, HttpStatusCode.UnprocessableEntity, $"Produto {int.MaxValue} não existe.");
    }

    [Fact]
    public async Task CriarPedido_EstoqueInsuficiente_Retorna422ComDisponivelESolicitado()
    {
        var cliente = await CriarCliente();
        var produto = await CriarProduto(estoque: 2);

        var resposta = await Post(new
        {
            clienteId = cliente.Id,
            itens = new[] { new { produtoId = produto.Id, quantidade = 3 } }
        });

        await AssertProblema(resposta, HttpStatusCode.UnprocessableEntity,
            $"Estoque insuficiente para o produto {produto.Id} ({produto.Nome}): disponível 2, solicitado 3.");
    }

    [Fact]
    public async Task CriarPedido_UmItemSemEstoque_RejeitaOPedidoInteiroENaoAlteraEstoque()
    {
        var cliente = await CriarCliente();
        var comEstoque = await CriarProduto(estoque: 10);
        var semEstoque = await CriarProduto(estoque: 0);

        var resposta = await Post(new
        {
            clienteId = cliente.Id,
            itens = new[]
            {
                new { produtoId = comEstoque.Id, quantidade = 4 },
                new { produtoId = semEstoque.Id, quantidade = 1 }
            }
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal(10, await Estoque(comEstoque.Id));
        Assert.Equal(0, await Estoque(semEstoque.Id));

        await using var db = api.CriarDbContext();
        Assert.False(await db.Pedidos.AnyAsync(p => p.ClienteId == cliente.Id));
    }

    // ---- apoio ----

    private Task<HttpResponseMessage> Post(object corpo) => api.Client.PostAsJsonAsync("/pedidos", corpo);

    private static async Task AssertProblema(HttpResponseMessage resposta, HttpStatusCode status, string mensagem)
    {
        Assert.Equal(status, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var problema = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(mensagem, problema.GetProperty("detail").GetString());
    }

    private async Task<Cliente> CriarCliente()
    {
        var id = Guid.NewGuid().ToString("N");
        var cliente = new Cliente { Nome = $"Cliente {id}", Email = $"{id}@teste.com", DataCadastro = DateTime.UtcNow };
        await using var db = api.CriarDbContext();
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();
        return cliente;
    }

    private async Task<Produto> CriarProduto(int estoque, decimal preco = 10.00m)
    {
        var produto = new Produto { Nome = $"Produto {Guid.NewGuid():N}", Preco = preco, Estoque = estoque };
        await using var db = api.CriarDbContext();
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        return produto;
    }

    private async Task<int> Estoque(int produtoId)
    {
        await using var db = api.CriarDbContext();
        return await db.Produtos.Where(p => p.Id == produtoId).Select(p => p.Estoque).SingleAsync();
    }
}
