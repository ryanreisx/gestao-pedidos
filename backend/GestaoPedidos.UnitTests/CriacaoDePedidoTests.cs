using GestaoPedidos.Api.Dados;
using GestaoPedidos.Api.Dominio;

namespace GestaoPedidos.UnitTests;

public class CriacaoDePedidoTests
{
    private static readonly DateTime Agora = new(2026, 11, 15, 14, 30, 0, DateTimeKind.Utc);
    private static readonly Cliente Cliente = new() { Id = 1, Nome = "Maria Silva" };

    private static Dictionary<int, Produto> Produtos(params Produto[] produtos) =>
        produtos.ToDictionary(p => p.Id);

    private static Produto Teclado(int estoque = 10) =>
        new() { Id = 2, Nome = "Teclado", Preco = 50.00m, Estoque = estoque };

    private static Produto Mouse(int estoque = 10) =>
        new() { Id = 3, Nome = "Mouse", Preco = 19.90m, Estoque = estoque };

    // ---- ValidarEntrada (400) ----

    [Fact]
    public void ValidarEntrada_PedidoValido_NaoRetornaErro()
    {
        var erro = CriacaoDePedido.ValidarEntrada(1, [new(2, 3), new(3, 1)]);

        Assert.Null(erro);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidarEntrada_SemClienteId_RetornaErro(int clienteId)
    {
        var erro = CriacaoDePedido.ValidarEntrada(clienteId, [new(2, 3)]);

        Assert.Equal("clienteId é obrigatório.", erro);
    }

    [Fact]
    public void ValidarEntrada_ItensNulos_RetornaErro()
    {
        var erro = CriacaoDePedido.ValidarEntrada(1, null);

        Assert.Equal("O pedido precisa ter pelo menos 1 item.", erro);
    }

    [Fact]
    public void ValidarEntrada_SemItens_RetornaErro()
    {
        var erro = CriacaoDePedido.ValidarEntrada(1, []);

        Assert.Equal("O pedido precisa ter pelo menos 1 item.", erro);
    }

    [Fact]
    public void ValidarEntrada_ItemSemProdutoId_RetornaErro()
    {
        var erro = CriacaoDePedido.ValidarEntrada(1, [new(0, 3)]);

        Assert.Equal("Selecione o produto do item 1.", erro);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ValidarEntrada_QuantidadeZeroOuNegativa_RetornaErro(int quantidade)
    {
        var erro = CriacaoDePedido.ValidarEntrada(1, [new(2, quantidade)]);

        Assert.Equal("A quantidade do item 1 deve ser maior que zero.", erro);
    }

    [Fact]
    public void ValidarEntrada_ProdutoRepetido_RetornaErro()
    {
        var erro = CriacaoDePedido.ValidarEntrada(1, [new(2, 1), new(3, 1), new(2, 4)]);

        Assert.Equal("O item 3 repete um produto que já está no pedido.", erro);
    }

    [Fact]
    public void ValidarEntrada_VariosProblemas_RetornaSoOPrimeiro()
    {
        var erro = CriacaoDePedido.ValidarEntrada(0, []);

        Assert.Equal("clienteId é obrigatório.", erro);
    }

    // ---- Montar (422) ----

    [Fact]
    public void Montar_ClienteInexistente_RetornaErro()
    {
        var resultado = CriacaoDePedido.Montar(99, null, [new(2, 1)], Produtos(Teclado()), Agora);

        Assert.Null(resultado.Pedido);
        Assert.Equal("Cliente 99 não existe.", resultado.Erro);
    }

    [Fact]
    public void Montar_ProdutoInexistente_RetornaErro()
    {
        var resultado = CriacaoDePedido.Montar(1, Cliente, [new(2, 1), new(77, 1)], Produtos(Teclado()), Agora);

        Assert.Null(resultado.Pedido);
        Assert.Equal("Produto 77 não existe.", resultado.Erro);
    }

    [Fact]
    public void Montar_EstoqueInsuficiente_RetornaErroComDisponivelESolicitado()
    {
        var resultado = CriacaoDePedido.Montar(1, Cliente, [new(2, 5)], Produtos(Teclado(estoque: 4)), Agora);

        Assert.Null(resultado.Pedido);
        Assert.Equal(
            "Estoque insuficiente para o produto Teclado: disponível 4, solicitado 5.",
            resultado.Erro);
    }

    [Fact]
    public void Montar_UmItemSemEstoque_RejeitaOPedidoInteiro()
    {
        var resultado = CriacaoDePedido.Montar(
            1, Cliente, [new(2, 1), new(3, 1)], Produtos(Teclado(), Mouse(estoque: 0)), Agora);

        Assert.Null(resultado.Pedido);
        Assert.Contains("Estoque insuficiente para o produto Mouse", resultado.Erro);
    }

    [Fact]
    public void Montar_QuantidadeIgualAoEstoque_CriaPedido()
    {
        var resultado = CriacaoDePedido.Montar(1, Cliente, [new(2, 4)], Produtos(Teclado(estoque: 4)), Agora);

        Assert.Null(resultado.Erro);
        Assert.NotNull(resultado.Pedido);
    }

    [Fact]
    public void Montar_VariosProblemas_RetornaSoOPrimeiro()
    {
        var resultado = CriacaoDePedido.Montar(99, null, [new(77, 1)], Produtos(), Agora);

        Assert.Equal("Cliente 99 não existe.", resultado.Erro);
    }

    [Fact]
    public void Montar_PedidoValido_CopiaPrecoDoProdutoParaCadaItem()
    {
        var resultado = CriacaoDePedido.Montar(
            1, Cliente, [new(2, 3), new(3, 2)], Produtos(Teclado(), Mouse()), Agora);

        var itens = resultado.Pedido!.Itens;
        Assert.Equal(2, itens.Count);
        Assert.Equal((2, 3, 50.00m), (itens[0].ProdutoId, itens[0].Quantidade, itens[0].PrecoUnitario));
        Assert.Equal((3, 2, 19.90m), (itens[1].ProdutoId, itens[1].Quantidade, itens[1].PrecoUnitario));
    }

    [Fact]
    public void Montar_PedidoValido_ValorTotalEhASomaDosItens()
    {
        var resultado = CriacaoDePedido.Montar(
            1, Cliente, [new(2, 3), new(3, 2)], Produtos(Teclado(), Mouse()), Agora);

        // 3 x 50,00 + 2 x 19,90
        Assert.Equal(189.80m, resultado.Pedido!.ValorTotal);
    }

    [Fact]
    public void Montar_PedidoValido_UsaClienteEDataInformados()
    {
        var resultado = CriacaoDePedido.Montar(1, Cliente, [new(2, 1)], Produtos(Teclado()), Agora);

        Assert.Equal(1, resultado.Pedido!.ClienteId);
        Assert.Equal(Agora, resultado.Pedido.DataPedido);
    }

    [Fact]
    public void Montar_PedidoValido_NaoAlteraOEstoqueDoProduto()
    {
        var teclado = Teclado(estoque: 10);

        CriacaoDePedido.Montar(1, Cliente, [new(2, 3)], Produtos(teclado), Agora);

        // A baixa é feita no banco, pelo endpoint.
        Assert.Equal(10, teclado.Estoque);
    }
}
