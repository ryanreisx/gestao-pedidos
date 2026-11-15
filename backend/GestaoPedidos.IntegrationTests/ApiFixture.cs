using GestaoPedidos.Api.Dados;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace GestaoPedidos.IntegrationTests;

// Sobe um SQL Server real em contêiner, aplica database/01-schema.sql e
// inicia a API apontando para ele. Compartilhada por todos os testes.
public class ApiFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private WebApplicationFactory<Program> _factory = null!;
    private string _connectionString = "";

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();

        var schema = await File.ReadAllTextAsync(CaminhoDoSchema());
        var resultado = await _sqlServer.ExecScriptAsync(schema);
        if (resultado.ExitCode != 0)
            throw new InvalidOperationException($"Falha ao aplicar o schema: {resultado.Stdout}{resultado.Stderr}");

        _connectionString = new SqlConnectionStringBuilder(_sqlServer.GetConnectionString())
        {
            InitialCatalog = "GestaoPedidos"
        }.ConnectionString;

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Default", _connectionString));
        Client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }

    // Acesso direto ao banco, para os testes criarem e conferirem dados.
    public AppDbContext CriarDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_connectionString).Options);

    // Cada teste cria os próprios dados, com nomes únicos, e não depende da carga inicial.

    public async Task<Cliente> CriarCliente(string? nome = null)
    {
        var id = Guid.NewGuid().ToString("N");
        var cliente = new Cliente { Nome = nome ?? $"Cliente {id}", Email = $"{id}@teste.com", DataCadastro = DateTime.UtcNow };
        await using var db = CriarDbContext();
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();
        return cliente;
    }

    public async Task<Produto> CriarProduto(int estoque, decimal preco = 10.00m)
    {
        var produto = new Produto { Nome = $"Produto {Guid.NewGuid():N}", Preco = preco, Estoque = estoque };
        await using var db = CriarDbContext();
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        return produto;
    }

    // Grava um pedido direto no banco, com data fixa, sem passar pelo POST.
    public async Task<Pedido> CriarPedido(int clienteId, DateTime data, params (Produto Produto, int Quantidade)[] itens)
    {
        var pedido = new Pedido
        {
            ClienteId = clienteId,
            DataPedido = data,
            Itens = itens
                .Select(i => new ItemPedido { ProdutoId = i.Produto.Id, Quantidade = i.Quantidade, PrecoUnitario = i.Produto.Preco })
                .ToList()
        };
        pedido.ValorTotal = pedido.Itens.Sum(i => i.Quantidade * i.PrecoUnitario);
        await using var db = CriarDbContext();
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();
        return pedido;
    }

    private static string CaminhoDoSchema()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null)
        {
            var caminho = Path.Combine(pasta.FullName, "database", "01-schema.sql");
            if (File.Exists(caminho)) return caminho;
            pasta = pasta.Parent;
        }
        throw new FileNotFoundException("database/01-schema.sql não encontrado a partir da pasta de testes.");
    }
}

[CollectionDefinition(Nome)]
public class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Nome = "Api";
}
