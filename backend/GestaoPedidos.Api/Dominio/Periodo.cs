namespace GestaoPedidos.Api.Dominio;

// Filtro de período usado na listagem de pedidos e no relatório.
public static class Periodo
{
    // Retorna a mensagem do problema ou null se estiver tudo certo.
    public static string? Validar(DateOnly? dataInicio, DateOnly? dataFim)
    {
        if (dataInicio is not null && dataFim is not null && dataInicio > dataFim)
            return "A data inicial não pode ser maior que a data final.";

        return null;
    }

    // Converte as datas em um intervalo de instantes UTC: [De, Ate).
    // Os dois dias entram inteiros, por isso Ate é o início do dia seguinte a dataFim.
    public static (DateTime? De, DateTime? Ate) ParaIntervaloUtc(DateOnly? dataInicio, DateOnly? dataFim)
    {
        DateTime? de = dataInicio?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime? ate = dataFim?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return (de, ate);
    }
}
