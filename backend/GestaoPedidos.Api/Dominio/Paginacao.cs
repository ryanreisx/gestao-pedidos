namespace GestaoPedidos.Api.Dominio;

public static class Paginacao
{
    public const int TamanhoMaximo = 50;

    // Retorna a mensagem do primeiro problema ou null se estiver tudo certo.
    public static string? Validar(int page, int pageSize)
    {
        if (page < 1)
            return "page deve ser maior ou igual a 1.";

        if (pageSize < 1 || pageSize > TamanhoMaximo)
            return $"pageSize deve estar entre 1 e {TamanhoMaximo}.";

        // Evita estouro no cálculo de quantos registros pular.
        if ((long)(page - 1) * pageSize > int.MaxValue)
            return "page é grande demais.";

        return null;
    }
}
