using GestaoPedidos.Api.Dados;
using GestaoPedidos.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

app.MapClientesEndpoints();
app.MapProdutosEndpoints();
app.MapPedidosEndpoints();

app.Run();
