-- Parte 4: ranking dos clientes que mais compraram em 2026.
-- Query original (lenta e com resultado incorreto), a corrigida e uma alternativa mais barata.
-- A explicação e as medições estão no README.
--
-- Para medir, rode depois de database/debug/carga-volume.sql, com:
--   SET STATISTICS IO ON;  SET STATISTICS TIME ON;
-- Para ver o plano estimado em texto: SET SHOWPLAN_TEXT ON; (sozinho no lote, seguido de GO)

USE GestaoPedidos;
GO

-- 1) ORIGINAL -------------------------------------------------------------------------
SELECT c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Clientes c
LEFT JOIN Pedidos p ON p.ClienteId = c.Id
LEFT JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE YEAR(p.DataPedido) = 2026
GROUP BY c.Nome
ORDER BY Total;
GO

-- 2) CORRIGIDA ------------------------------------------------------------------------
--   - ORDER BY ... DESC: ranking é do maior para o menor.
--   - GROUP BY c.Id, c.Nome: clientes com o mesmo nome não são somados juntos.
--   - Intervalo de datas no lugar de YEAR(): permite busca no índice de DataPedido.
--   - INNER JOIN: o WHERE em p.DataPedido já descartava quem não tem pedido; o LEFT JOIN
--     só escondia isso.
SELECT c.Id, c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Pedidos p
JOIN Clientes c ON c.Id = p.ClienteId
JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE p.DataPedido >= '2026-01-01' AND p.DataPedido < '2027-01-01'
GROUP BY c.Id, c.Nome
ORDER BY Total DESC;
GO

-- 3) ALTERNATIVA ----------------------------------------------------------------------
--   Pedidos.ValorTotal já é a soma dos itens (regra de negócio 9), então dá para
--   dispensar a tabela ItensPedido. O índice IX_Pedidos_DataPedido cobre a consulta.
SELECT c.Id, c.Nome, SUM(p.ValorTotal) AS Total
FROM Pedidos p
JOIN Clientes c ON c.Id = p.ClienteId
WHERE p.DataPedido >= '2026-01-01' AND p.DataPedido < '2027-01-01'
GROUP BY c.Id, c.Nome
ORDER BY Total DESC;
GO
