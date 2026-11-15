using GestaoPedidos.Api.Dados;
using GestaoPedidos.Api.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Mini Gestão de Pedidos — API",
        Version = "v1",
        Description = "API de cadastro e consulta de pedidos de clientes, catálogo de clientes/produtos e relatório dos clientes que mais compraram."
    });
});
builder.Services.AddProblemDetails();
// Corpo ou parâmetro em formato inválido responde 400 em todos os ambientes.
// (Em Development o padrão é lançar exceção, que viraria 500.)
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

// Erros não tratados e respostas de erro sem corpo saem em ProblemDetails.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Mini Gestão de Pedidos v1");
    options.DocumentTitle = "Mini Gestão de Pedidos — Swagger";
});

app.MapClientesEndpoints();
app.MapProdutosEndpoints();
app.MapPedidosEndpoints();
app.MapRelatoriosEndpoints();

app.Run();
