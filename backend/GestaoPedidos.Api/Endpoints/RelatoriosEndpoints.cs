using GestaoPedidos.Api.Dados;
using GestaoPedidos.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace GestaoPedidos.Api.Endpoints;

public record TopClienteResponse(int ClienteId, string Nome, decimal ValorTotal, int QuantidadePedidos);

public static class RelatoriosEndpoints
{
    private const int MaximoDeClientes = 5;

    public static void MapRelatoriosEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/relatorios/top-clientes", TopClientes);
    }

    private static async Task<IResult> TopClientes(AppDbContext db, DateOnly? dataInicio, DateOnly? dataFim)
    {
        var erro = Periodo.Validar(dataInicio, dataFim);
        if (erro is not null)
            return Results.Problem(detail: erro, statusCode: StatusCodes.Status400BadRequest);

        var (de, ate) = Periodo.ParaIntervaloUtc(dataInicio, dataFim);

        var pedidos = db.Pedidos.AsNoTracking();
        if (de is not null) pedidos = pedidos.Where(p => p.DataPedido >= de);
        if (ate is not null) pedidos = pedidos.Where(p => p.DataPedido < ate);

        // Agrupa por Id (e não só por nome) para não somar clientes homônimos.
        var top = await pedidos
            .GroupBy(p => new { p.ClienteId, p.Cliente!.Nome })
            .Select(g => new
            {
                g.Key.ClienteId,
                g.Key.Nome,
                ValorTotal = g.Sum(p => p.ValorTotal),
                QuantidadePedidos = g.Count()
            })
            .OrderByDescending(c => c.ValorTotal)
            .ThenByDescending(c => c.QuantidadePedidos)
            .ThenBy(c => c.Nome)
            .ThenBy(c => c.ClienteId)
            .Take(MaximoDeClientes)
            .ToListAsync();

        return Results.Ok(top.Select(c => new TopClienteResponse(c.ClienteId, c.Nome, c.ValorTotal, c.QuantidadePedidos)));
    }
}
