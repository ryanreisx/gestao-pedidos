using GestaoPedidos.Api.Dados;
using GestaoPedidos.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace GestaoPedidos.Api.Endpoints;

public record ItemPedidoRequest(int ProdutoId, int Quantidade);
public record PedidoRequest(int ClienteId, List<ItemPedidoRequest>? Itens);

public record ItemPedidoResponse(int ProdutoId, string ProdutoNome, int Quantidade, decimal PrecoUnitario);
public record PedidoResponse(
    int Id,
    int ClienteId,
    string ClienteNome,
    DateTime DataPedido,
    decimal ValorTotal,
    List<ItemPedidoResponse> Itens);

public static class PedidosEndpoints
{
    public static void MapPedidosEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/pedidos", CriarPedido);
    }

    private static async Task<IResult> CriarPedido(PedidoRequest request, AppDbContext db)
    {
        // 1. Validação de entrada (400).
        var itens = request.Itens?.Select(i => new ItemSolicitado(i.ProdutoId, i.Quantidade)).ToList();
        var erroDeEntrada = CriacaoDePedido.ValidarEntrada(request.ClienteId, itens);
        if (erroDeEntrada is not null)
            return Results.Problem(detail: erroDeEntrada, statusCode: StatusCodes.Status400BadRequest);

        // 2. Regras de negócio (422).
        var cliente = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.ClienteId);
        var idsDosProdutos = itens!.Select(i => i.ProdutoId).ToList();
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => idsDosProdutos.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var resultado = CriacaoDePedido.Montar(request.ClienteId, cliente, itens!, produtos, DateTime.UtcNow);
        if (resultado.Pedido is null)
            return Results.Problem(detail: resultado.Erro, statusCode: StatusCodes.Status422UnprocessableEntity);

        var pedido = resultado.Pedido;

        // 3. Baixa de estoque e gravação na mesma transação.
        await using var transacao = await db.Database.BeginTransactionAsync();

        // UPDATE condicional: só baixa se ainda houver estoque, o que cobre pedidos simultâneos.
        // A ordem fixa por produto evita deadlock entre pedidos com os mesmos produtos.
        foreach (var item in pedido.Itens.OrderBy(i => i.ProdutoId))
        {
            var linhasAfetadas = await db.Produtos
                .Where(p => p.Id == item.ProdutoId && p.Estoque >= item.Quantidade)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Estoque, p => p.Estoque - item.Quantidade));

            if (linhasAfetadas == 0)
            {
                await transacao.RollbackAsync();
                var produto = produtos[item.ProdutoId];
                return Results.Problem(
                    detail: $"Estoque insuficiente para o produto {produto.Id} ({produto.Nome}).",
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        }

        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();
        await transacao.CommitAsync();

        // 4. Resposta.
        var response = new PedidoResponse(
            pedido.Id,
            pedido.ClienteId,
            cliente!.Nome,
            pedido.DataPedido,
            pedido.ValorTotal,
            pedido.Itens
                .Select(i => new ItemPedidoResponse(i.ProdutoId, produtos[i.ProdutoId].Nome, i.Quantidade, i.PrecoUnitario))
                .ToList());

        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }
}
