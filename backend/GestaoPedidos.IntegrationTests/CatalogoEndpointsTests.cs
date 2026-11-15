using System.Net.Http.Json;
using GestaoPedidos.Api.Dados;
using GestaoPedidos.Api.Endpoints;

namespace GestaoPedidos.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class CatalogoEndpointsTests(ApiFixture api)
{
    [Fact]
    public async Task ListarProdutos_ComProdutosCadastrados_RetornaEmOrdemAlfabetica()
    {
        var prefixo = $"Produto {Guid.NewGuid():N}";
        await using (var db = api.CriarDbContext())
        {
            db.Produtos.AddRange(
                new Produto { Nome = $"{prefixo} B", Preco = 10.50m, Estoque = 3 },
                new Produto { Nome = $"{prefixo} A", Preco = 99.90m, Estoque = 0 });
            await db.SaveChangesAsync();
        }

        var produtos = await api.Client.GetFromJsonAsync<List<ProdutoResponse>>("/produtos");

        var criados = produtos!.Where(p => p.Nome.StartsWith(prefixo)).ToList();
        Assert.Equal([$"{prefixo} A", $"{prefixo} B"], criados.Select(p => p.Nome));
        Assert.Equal(99.90m, criados[0].Preco);
        Assert.Equal(0, criados[0].Estoque);
        Assert.Equal(10.50m, criados[1].Preco);
        Assert.Equal(3, criados[1].Estoque);
    }

    [Fact]
    public async Task ListarClientes_ComClientesCadastrados_RetornaEmOrdemAlfabetica()
    {
        var prefixo = $"Cliente {Guid.NewGuid():N}";
        await using (var db = api.CriarDbContext())
        {
            db.Clientes.AddRange(
                new Cliente { Nome = $"{prefixo} B", Email = $"{Guid.NewGuid():N}@teste.com", DataCadastro = DateTime.UtcNow },
                new Cliente { Nome = $"{prefixo} A", Email = $"{Guid.NewGuid():N}@teste.com", DataCadastro = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var clientes = await api.Client.GetFromJsonAsync<List<ClienteResponse>>("/clientes");

        var criados = clientes!.Where(c => c.Nome.StartsWith(prefixo)).ToList();
        Assert.Equal([$"{prefixo} A", $"{prefixo} B"], criados.Select(c => c.Nome));
        Assert.All(criados, c => Assert.EndsWith("@teste.com", c.Email));
    }
}
