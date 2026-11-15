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
                .ToListAsync())
            .WithTags("Produtos")
            .WithSummary("Lista todos os produtos")
            .WithDescription("Retorna todos os produtos em ordem alfabética, com preço unitário e estoque disponível.")
            .Produces<List<ProdutoResponse>>(StatusCodes.Status200OK);
    }
}
