using GestaoPedidos.Api.Dados;
using Microsoft.EntityFrameworkCore;

namespace GestaoPedidos.Api.Endpoints;

public record ProdutoResponse(int Id, string Nome, decimal Preco, int Estoque);

public static class ProdutosEndpoints
{
    public static void MapProdutosEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/produtos", async (AppDbContext db) =>
            await db.Produtos
                .AsNoTracking()
                .OrderBy(p => p.Nome).ThenBy(p => p.Id)
                .Select(p => new ProdutoResponse(p.Id, p.Nome, p.Preco, p.Estoque))
                .ToListAsync());
    }
}
