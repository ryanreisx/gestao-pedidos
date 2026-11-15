-- Cria o banco e as tabelas da Mini Gestão de Pedidos.
-- Idempotente: pode rodar várias vezes sem erro e sem alterar o que já existe.

IF DB_ID('GestaoPedidos') IS NULL
    CREATE DATABASE GestaoPedidos;
GO

USE GestaoPedidos;
GO

IF OBJECT_ID('dbo.Clientes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clientes (
        Id           INT IDENTITY(1,1) NOT NULL,
        Nome         NVARCHAR(150)     NOT NULL,
        Email        NVARCHAR(254)     NOT NULL,
        DataCadastro DATETIME2         NOT NULL CONSTRAINT DF_Clientes_DataCadastro DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_Clientes PRIMARY KEY (Id),
        CONSTRAINT UQ_Clientes_Email UNIQUE (Email)
    );
END
GO

IF OBJECT_ID('dbo.Produtos', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Produtos (
        Id      INT IDENTITY(1,1) NOT NULL,
        Nome    NVARCHAR(150)     NOT NULL,
        Preco   DECIMAL(18,2)     NOT NULL,
        Estoque INT               NOT NULL,
        CONSTRAINT PK_Produtos PRIMARY KEY (Id),
        CONSTRAINT CK_Produtos_Preco CHECK (Preco >= 0),
        CONSTRAINT CK_Produtos_Estoque CHECK (Estoque >= 0)
    );
END
GO

IF OBJECT_ID('dbo.Pedidos', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Pedidos (
        Id         INT IDENTITY(1,1) NOT NULL,
        ClienteId  INT               NOT NULL,
        DataPedido DATETIME2         NOT NULL,
        ValorTotal DECIMAL(18,2)     NOT NULL,
        CONSTRAINT PK_Pedidos PRIMARY KEY (Id),
        CONSTRAINT FK_Pedidos_Clientes FOREIGN KEY (ClienteId) REFERENCES dbo.Clientes (Id)
    );

    -- Listagem e relatório por período, do mais recente para o mais antigo.
    CREATE INDEX IX_Pedidos_DataPedido
        ON dbo.Pedidos (DataPedido DESC)
        INCLUDE (ClienteId, ValorTotal);

    -- Listagem filtrada por cliente (e índice da FK).
    CREATE INDEX IX_Pedidos_ClienteId_DataPedido
        ON dbo.Pedidos (ClienteId, DataPedido DESC);
END
GO

IF OBJECT_ID('dbo.ItensPedido', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ItensPedido (
        Id            INT IDENTITY(1,1) NOT NULL,
        PedidoId      INT               NOT NULL,
        ProdutoId     INT               NOT NULL,
        Quantidade    INT               NOT NULL,
        PrecoUnitario DECIMAL(18,2)     NOT NULL,
        CONSTRAINT PK_ItensPedido PRIMARY KEY (Id),
        CONSTRAINT FK_ItensPedido_Pedidos FOREIGN KEY (PedidoId) REFERENCES dbo.Pedidos (Id),
        CONSTRAINT FK_ItensPedido_Produtos FOREIGN KEY (ProdutoId) REFERENCES dbo.Produtos (Id),
        CONSTRAINT CK_ItensPedido_Quantidade CHECK (Quantidade > 0)
    );

    -- Um produto por pedido; cobre a busca dos itens de um pedido e a soma por pedido.
    CREATE UNIQUE INDEX UX_ItensPedido_PedidoId_ProdutoId
        ON dbo.ItensPedido (PedidoId, ProdutoId)
        INCLUDE (Quantidade, PrecoUnitario);

    -- Índice da FK de produto.
    CREATE INDEX IX_ItensPedido_ProdutoId
        ON dbo.ItensPedido (ProdutoId);
END
GO
