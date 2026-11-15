using GestaoPedidos.Api.Dominio;

namespace GestaoPedidos.UnitTests;

public class PeriodoTests
{
    private static readonly DateOnly Dia10 = new(2026, 3, 10);
    private static readonly DateOnly Dia12 = new(2026, 3, 12);

    [Fact]
    public void Validar_SemDatas_NaoRetornaErro()
    {
        Assert.Null(Periodo.Validar(null, null));
    }

    [Fact]
    public void Validar_SoUmaDasDatas_NaoRetornaErro()
    {
        Assert.Null(Periodo.Validar(Dia10, null));
        Assert.Null(Periodo.Validar(null, Dia12));
    }

    [Fact]
    public void Validar_InicioIgualAoFim_NaoRetornaErro()
    {
        Assert.Null(Periodo.Validar(Dia10, Dia10));
    }

    [Fact]
    public void Validar_InicioMaiorQueFim_RetornaErro()
    {
        Assert.Equal("dataInicio não pode ser maior que dataFim.", Periodo.Validar(Dia12, Dia10));
    }

    [Fact]
    public void ParaIntervaloUtc_ComAsDuasDatas_IncluiOsDoisDiasInteiros()
    {
        var (de, ate) = Periodo.ParaIntervaloUtc(Dia10, Dia12);

        Assert.Equal(new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc), de);
        Assert.Equal(new DateTime(2026, 3, 13, 0, 0, 0, DateTimeKind.Utc), ate);
        Assert.Equal(DateTimeKind.Utc, de!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, ate!.Value.Kind);
    }

    [Fact]
    public void ParaIntervaloUtc_SoComInicio_NaoLimitaOFim()
    {
        var (de, ate) = Periodo.ParaIntervaloUtc(Dia10, null);

        Assert.Equal(new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc), de);
        Assert.Null(ate);
    }

    [Fact]
    public void ParaIntervaloUtc_SoComFim_NaoLimitaOInicio()
    {
        var (de, ate) = Periodo.ParaIntervaloUtc(null, Dia12);

        Assert.Null(de);
        Assert.Equal(new DateTime(2026, 3, 13, 0, 0, 0, DateTimeKind.Utc), ate);
    }

    [Fact]
    public void ParaIntervaloUtc_SemDatas_NaoLimitaNada()
    {
        var (de, ate) = Periodo.ParaIntervaloUtc(null, null);

        Assert.Null(de);
        Assert.Null(ate);
    }
}
