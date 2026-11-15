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
