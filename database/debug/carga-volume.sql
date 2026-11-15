-- Carga de volume para a investigação da Parte 4 (debug da query de ranking).
-- Gera 200 mil pedidos, com 2 itens cada, entre 2023 e 2026.
--
-- NÃO roda no docker compose up (o db-init só executa os scripts da raiz de database/).
-- Rodar uma vez, à mão, depois da carga inicial:
--
--   docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd \
--     -S localhost -U sa -P GestaoPedidos123 -C -b < database/debug/carga-volume.sql
--
-- Para voltar ao banco só com a carga inicial: docker compose down -v && docker compose up

USE GestaoPedidos;
GO

SET NOCOUNT ON;

DECLARE @ultimoId INT = (SELECT ISNULL(MAX(Id), 0) FROM dbo.Pedidos);

-- Pedidos: cliente distribuído entre os que já compram (o cliente 9 continua sem pedidos)
-- e data aleatória entre 01/01/2023 e 14/11/2026.
INSERT INTO dbo.Pedidos (ClienteId, DataPedido, ValorTotal)
SELECT
    CHOOSE(1 + n.Numero % 9, 1, 2, 3, 4, 5, 6, 7, 8, 10),
    DATEADD(SECOND, ABS(CHECKSUM(NEWID())) % 122000000, '2023-01-01'),
    0
FROM (
    SELECT TOP (200000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Numero
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
) n;

-- Dois itens por pedido, de produtos diferentes, com o preço copiado do produto.
INSERT INTO dbo.ItensPedido (PedidoId, ProdutoId, Quantidade, PrecoUnitario)
SELECT p.Id, pr.Id, 1 + (p.Id + d.Deslocamento) % 3, pr.Preco
FROM dbo.Pedidos p
CROSS JOIN (VALUES (0), (3)) AS d (Deslocamento)
JOIN dbo.Produtos pr ON pr.Id = 1 + (p.Id + d.Deslocamento) % 10
WHERE p.Id > @ultimoId;

UPDATE p
SET ValorTotal = t.Total
FROM dbo.Pedidos p
JOIN (
    SELECT PedidoId, SUM(Quantidade * PrecoUnitario) AS Total
    FROM dbo.ItensPedido
    GROUP BY PedidoId
) t ON t.PedidoId = p.Id
WHERE p.Id > @ultimoId;

SELECT
    (SELECT COUNT(*) FROM dbo.Pedidos) AS Pedidos,
    (SELECT COUNT(*) FROM dbo.ItensPedido) AS ItensPedido;
GO
