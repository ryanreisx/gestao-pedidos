using GestaoPedidos.Api.Dados;

namespace GestaoPedidos.Api.Dominio;

public record ItemSolicitado(int ProdutoId, int Quantidade);

// Pedido montado ou a mensagem do primeiro erro encontrado; nunca os dois.
public record ResultadoPedido(Pedido? Pedido, string? Erro);

// Regras de criação de pedido. Funções puras: recebem os dados já carregados.
public static class CriacaoDePedido
{
    // Erros de entrada (400). Retorna a mensagem do primeiro problema ou null se estiver tudo certo.
    public static string? ValidarEntrada(int clienteId, IReadOnlyList<ItemSolicitado>? itens)
    {
        if (clienteId <= 0)
            return "clienteId é obrigatório.";

        if (itens is null || itens.Count == 0)
            return "O pedido precisa ter pelo menos 1 item.";

        var produtosVistos = new HashSet<int>();
        foreach (var item in itens)
        {
            if (item.ProdutoId <= 0)
                return "produtoId é obrigatório em todos os itens.";

            if (item.Quantidade <= 0)
                return $"A quantidade do produto {item.ProdutoId} deve ser maior que zero.";

            if (!produtosVistos.Add(item.ProdutoId))
                return $"O produto {item.ProdutoId} aparece em mais de um item.";
        }

        return null;
    }

    // Regras de negócio (422). Espera itens já aprovados por ValidarEntrada.
    public static ResultadoPedido Montar(
        int clienteId,
        Cliente? cliente,
        IReadOnlyList<ItemSolicitado> itens,
        IReadOnlyDictionary<int, Produto> produtos,
        DateTime agora)
    {
        if (cliente is null)
            return new(null, $"Cliente {clienteId} não existe.");

        var pedido = new Pedido { ClienteId = cliente.Id, DataPedido = agora };

        foreach (var item in itens)
        {
            if (!produtos.TryGetValue(item.ProdutoId, out var produto))
                return new(null, $"Produto {item.ProdutoId} não existe.");

            if (produto.Estoque < item.Quantidade)
                return new(null,
                    $"Estoque insuficiente para o produto {produto.Id} ({produto.Nome}): " +
                    $"disponível {produto.Estoque}, solicitado {item.Quantidade}.");

            pedido.Itens.Add(new ItemPedido
            {
                ProdutoId = produto.Id,
                Quantidade = item.Quantidade,
                PrecoUnitario = produto.Preco
            });
        }

        pedido.ValorTotal = pedido.Itens.Sum(i => i.Quantidade * i.PrecoUnitario);
        return new(pedido, null);
    }
}
