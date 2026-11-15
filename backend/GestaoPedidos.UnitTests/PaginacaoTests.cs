using GestaoPedidos.Api.Dominio;

namespace GestaoPedidos.UnitTests;

public class PaginacaoTests
{
    [Theory]
    [InlineData(1, 10)]
    [InlineData(1, 1)]
    [InlineData(7, 50)]
    public void Validar_ValoresDentroDosLimites_NaoRetornaErro(int page, int pageSize)
    {
        Assert.Null(Paginacao.Validar(page, pageSize));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validar_PageMenorQueUm_RetornaErro(int page)
    {
        Assert.Equal("page deve ser maior ou igual a 1.", Paginacao.Validar(page, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    public void Validar_PageSizeForaDeUmACinquenta_RetornaErro(int pageSize)
    {
        Assert.Equal("pageSize deve estar entre 1 e 50.", Paginacao.Validar(1, pageSize));
    }

    [Fact]
    public void Validar_PageGrandeDemais_RetornaErro()
    {
        Assert.Equal("page é grande demais.", Paginacao.Validar(int.MaxValue, 50));
    }
}
