namespace GestaoPedidos.Api.Dominio;

public static class Paginacao
{
    public const int TamanhoMaximo = 50;

    // Retorna a mensagem do primeiro problema ou null se estiver tudo certo.
    public static string? Validar(int page, int pageSize)
    {
        if (page < 1)
            return "A página deve ser maior ou igual a 1.";

        if (pageSize < 1 || pageSize > TamanhoMaximo)
            return $"A quantidade por página deve estar entre 1 e {TamanhoMaximo}.";

        // Evita estouro no cálculo de quantos registros pular.
        if ((long)(page - 1) * pageSize > int.MaxValue)
            return "O número da página é grande demais.";

        return null;
    }
}
