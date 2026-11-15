-- Carga de dados de exemplo. Só roda se a tabela Clientes estiver vazia.
--
-- Casos propositais:
--   - Produto 9 com estoque zero.
--   - Clientes 1 e 8 com o mesmo nome (e-mails diferentes).
--   - Cliente 9 sem nenhum pedido; cliente 10 só com pedidos de 2025.
--   - Pedidos em 2025 e 2026.

USE GestaoPedidos;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.Clientes)
BEGIN
    BEGIN TRANSACTION;

    SET IDENTITY_INSERT dbo.Clientes ON;
    INSERT INTO dbo.Clientes (Id, Nome, Email, DataCadastro) VALUES
        (1,  N'Ana Souza',        N'ana.souza@exemplo.com',        '2025-01-10T12:00:00'),
        (2,  N'Bruno Lima',       N'bruno.lima@exemplo.com',       '2025-01-15T12:00:00'),
        (3,  N'Carla Mendes',     N'carla.mendes@exemplo.com',     '2025-02-01T12:00:00'),
        (4,  N'Diego Santos',     N'diego.santos@exemplo.com',     '2025-02-20T12:00:00'),
        (5,  N'Elaine Rocha',     N'elaine.rocha@exemplo.com',     '2025-03-05T12:00:00'),
        (6,  N'Fábio Almeida',    N'fabio.almeida@exemplo.com',    '2025-04-12T12:00:00'),
        (7,  N'Gabriela Costa',   N'gabriela.costa@exemplo.com',   '2025-05-30T12:00:00'),
        (8,  N'Ana Souza',        N'ana.souza2@exemplo.com',       '2025-07-08T12:00:00'),
        (9,  N'Igor Martins',     N'igor.martins@exemplo.com',     '2025-09-14T12:00:00'),
        (10, N'Juliana Ribeiro',  N'juliana.ribeiro@exemplo.com',  '2025-02-11T12:00:00');
    SET IDENTITY_INSERT dbo.Clientes OFF;

    SET IDENTITY_INSERT dbo.Produtos ON;
    INSERT INTO dbo.Produtos (Id, Nome, Preco, Estoque) VALUES
        (1,  N'Teclado Mecânico',      250.00,  40),
        (2,  N'Mouse Sem Fio',          89.90, 120),
        (3,  N'Monitor 24 Polegadas',  899.00,  15),
        (4,  N'Headset USB',           199.90,  35),
        (5,  N'Webcam Full HD',        159.00,  25),
        (6,  N'Cadeira Ergonômica',   1299.00,   8),
        (7,  N'Mousepad Grande',        49.90, 200),
        (8,  N'Hub USB-C',             129.00,  60),
        (9,  N'Suporte para Notebook', 119.90,   0),
        (10, N'SSD 1TB',               449.00,  30);
    SET IDENTITY_INSERT dbo.Produtos OFF;

    -- ValorTotal entra zerado e é calculado a partir dos itens no fim.
    SET IDENTITY_INSERT dbo.Pedidos ON;
    INSERT INTO dbo.Pedidos (Id, ClienteId, DataPedido, ValorTotal) VALUES
        (1,  1,  '2025-03-12T14:20:00', 0),
        (2,  10, '2025-04-03T09:15:00', 0),
        (3,  2,  '2025-05-21T18:40:00', 0),
        (4,  3,  '2025-06-30T11:05:00', 0),
        (5,  10, '2025-08-17T16:30:00', 0),
        (6,  4,  '2025-09-09T13:45:00', 0),
        (7,  1,  '2025-10-25T20:10:00', 0),
        (8,  5,  '2025-11-28T10:00:00', 0),
        (9,  2,  '2025-12-15T15:25:00', 0),
        (10, 6,  '2025-12-31T23:30:00', 0),
        (11, 1,  '2026-01-01T00:15:00', 0),
        (12, 3,  '2026-01-14T12:00:00', 0),
        (13, 7,  '2026-01-29T17:50:00', 0),
        (14, 2,  '2026-02-10T08:30:00', 0),
        (15, 4,  '2026-02-26T19:20:00', 0),
        (16, 8,  '2026-03-08T14:00:00', 0),
        (17, 5,  '2026-03-22T10:45:00', 0),
        (18, 1,  '2026-04-05T16:10:00', 0),
        (19, 6,  '2026-04-19T09:00:00', 0),
        (20, 3,  '2026-05-07T13:30:00', 0),
        (21, 7,  '2026-05-25T21:15:00', 0),
        (22, 2,  '2026-06-11T11:40:00', 0),
        (23, 8,  '2026-06-28T15:05:00', 0),
        (24, 4,  '2026-07-16T18:00:00', 0),
        (25, 1,  '2026-08-02T10:20:00', 0),
        (26, 5,  '2026-08-20T14:55:00', 0),
        (27, 3,  '2026-09-13T12:10:00', 0),
        (28, 6,  '2026-10-04T17:35:00', 0),
        (29, 2,  '2026-10-27T09:50:00', 0),
        (30, 7,  '2026-11-10T16:25:00', 0);
    SET IDENTITY_INSERT dbo.Pedidos OFF;

    -- PrecoUnitario é copiado do produto, como na criação de um pedido.
    INSERT INTO dbo.ItensPedido (PedidoId, ProdutoId, Quantidade, PrecoUnitario)
    SELECT i.PedidoId, i.ProdutoId, i.Quantidade, p.Preco
    FROM (VALUES
        (1, 1, 1),   (1, 2, 2),
        (2, 3, 1),
        (3, 7, 3),   (3, 8, 1),
        (4, 9, 2),
        (5, 4, 1),   (5, 5, 1),
        (6, 6, 1),
        (7, 10, 2),
        (8, 2, 1),   (8, 7, 1),   (8, 9, 1),
        (9, 3, 2),
        (10, 1, 1),  (10, 4, 1),
        (11, 6, 1),  (11, 3, 1),
        (12, 2, 4),
        (13, 10, 1), (13, 8, 2),
        (14, 5, 2),
        (15, 1, 2),  (15, 7, 2),
        (16, 3, 1),  (16, 4, 1),
        (17, 9, 1),
        (18, 10, 3),
        (19, 2, 2),  (19, 8, 1),
        (20, 6, 2),
        (21, 7, 5),
        (22, 1, 1),  (22, 10, 1),
        (23, 5, 1),
        (24, 3, 1),  (24, 2, 1),  (24, 7, 1),
        (25, 4, 2),
        (26, 6, 1),  (26, 1, 1),
        (27, 8, 3),
        (28, 10, 2), (28, 5, 1),
        (29, 3, 3),
        (30, 2, 1),  (30, 4, 1)
    ) AS i (PedidoId, ProdutoId, Quantidade)
    JOIN dbo.Produtos p ON p.Id = i.ProdutoId;

    UPDATE ped
    SET ValorTotal = t.Total
    FROM dbo.Pedidos ped
    JOIN (
        SELECT PedidoId, SUM(Quantidade * PrecoUnitario) AS Total
        FROM dbo.ItensPedido
        GROUP BY PedidoId
    ) t ON t.PedidoId = ped.Id;

    COMMIT TRANSACTION;

    PRINT 'Carga inicial aplicada.';
END
ELSE
    PRINT 'Carga inicial ignorada: o banco já tem dados.';
GO
