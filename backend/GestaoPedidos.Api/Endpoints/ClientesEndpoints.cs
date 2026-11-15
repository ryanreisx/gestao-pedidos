using GestaoPedidos.Api.Dados;
using Microsoft.EntityFrameworkCore;

namespace GestaoPedidos.Api.Endpoints;

public record ClienteResponse(int Id, string Nome, string Email);

public static class ClientesEndpoints
{
    public static void MapClientesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/clientes", async (AppDbContext db) =>
            await db.Clientes
                .AsNoTracking()
                .OrderBy(c => c.Nome).ThenBy(c => c.Id)
                .Select(c => new ClienteResponse(c.Id, c.Nome, c.Email))
                .ToListAsync())
            .WithTags("Clientes")
            .WithSummary("Lista todos os clientes")
            .WithDescription("Retorna todos os clientes cadastrados em ordem alfabética (id, nome e e-mail).")
            .Produces<List<ClienteResponse>>(StatusCodes.Status200OK);
    }
}
