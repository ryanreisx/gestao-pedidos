# Mini Gestão de Pedidos

Aplicação de cadastro e consulta de pedidos de clientes.
Especificação em [docs/spec.md](docs/spec.md) e plano de implementação em [docs/plan.md](docs/plan.md).

## Como rodar com Docker

```bash
docker compose up --build
```

| Serviço | Endereço |
|---|---|
| API | http://localhost:5080 |
| SQL Server | localhost:1433 |

_A completar com o front._

## Como rodar os testes

_A preencher._

## Principais decisões técnicas

_A preencher._

## Como usei o Claude

_A preencher._

### MCP do SQL Server

_A preencher._

## Debug da query (Parte 4)

A query deveria retornar o ranking dos clientes que mais compraram em 2026:

```sql
SELECT c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Clientes c
LEFT JOIN Pedidos p ON p.ClienteId = c.Id
LEFT JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE YEAR(p.DataPedido) = 2026
GROUP BY c.Nome
ORDER BY Total
```

As três versões (original, corrigida e alternativa) estão em
[database/debug/ranking-2026.sql](database/debug/ranking-2026.sql).

### O que estava errado

| # | Problema | Efeito | Correção |
|---|---|---|---|
| 1 | `ORDER BY Total` sem `DESC` | O "ranking" sai do menor para o maior | `ORDER BY Total DESC` |
| 2 | `GROUP BY c.Nome` | Clientes diferentes com o mesmo nome são somados como um só | `GROUP BY c.Id, c.Nome` |
| 3 | `WHERE YEAR(p.DataPedido) = 2026` | A função sobre a coluna impede a busca no índice: o banco lê todos os pedidos e calcula o ano de cada um | Intervalo `p.DataPedido >= '2026-01-01' AND p.DataPedido < '2027-01-01'` |
| 4 | `LEFT JOIN` com filtro no `WHERE` | O filtro em `p.DataPedido` descarta as linhas sem pedido, então o `LEFT JOIN` se comporta como `INNER JOIN` e só engana quem lê | `JOIN` explícito, partindo de `Pedidos` |

Os problemas 1 e 2 são os de resultado incorreto; o 3 é o de lentidão; o 4 é de clareza.

Query corrigida:

```sql
SELECT c.Id, c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Pedidos p
JOIN Clientes c ON c.Id = p.ClienteId
JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE p.DataPedido >= '2026-01-01' AND p.DataPedido < '2027-01-01'
GROUP BY c.Id, c.Nome
ORDER BY Total DESC;
```

### Como foi investigado

A investigação foi executada pelo Claude Code, com o MCP do SQL Server, e revisada por mim.

1. **Resultado, com a carga inicial (30 pedidos).** A carga tem dois clientes chamados
   "Ana Souza" (ids 1 e 8) justamente para este caso. A query original devolveu 7 linhas, em
   ordem crescente, com uma única "Ana Souza" somando 5.202,70. A corrigida devolveu 8 linhas,
   em ordem decrescente, com as duas separadas: 3.944,80 (id 1) e 1.257,90 (id 8), que somam
   exatamente os 5.202,70.
2. **Volume.** Com 30 pedidos nenhuma query é lenta e o plano de execução não muda. O script
   [database/debug/carga-volume.sql](database/debug/carga-volume.sql) gera 200 mil pedidos e
   400 mil itens entre 2023 e 2026; cerca de 43 mil pedidos (22%) são de 2026.
3. **Índices.** Pelo MCP, conferi os índices existentes. Os que importam aqui:
   `IX_Pedidos_DataPedido (DataPedido) INCLUDE (ClienteId, ValorTotal)` e
   `UX_ItensPedido_PedidoId_ProdutoId (PedidoId, ProdutoId) INCLUDE (Quantidade, PrecoUnitario)`.
4. **Leituras.** Cada query foi rodada com `SET STATISTICS IO ON` e `SET STATISTICS TIME ON`
   (o MCP devolve as mensagens).
5. **Plano de execução.** Com `SET SHOWPLAN_TEXT ON`, por `sqlcmd` dentro do contêiner, porque
   esse comando precisa estar sozinho no lote.
6. **Sem os índices.** Para separar o ganho da reescrita do ganho dos índices, as mesmas
   queries foram medidas com os índices removidos dentro de uma transação, desfeita no fim.

### O que o plano de execução mostrou

- **Original:** percorre `Clientes` e, para cada cliente, busca **todos** os pedidos dele
  (`Index Seek` em `IX_Pedidos_ClienteId_DataPedido` só por cliente). Depois calcula
  `datepart(year, DataPedido)` para os 200 mil pedidos e só então filtra 2026 (`Filter`).
  O índice de data não é usado. Os operadores continuam como `Left Outer Join` e o plano é
  paralelo.
- **Corrigida:** começa por um `Index Seek` em `IX_Pedidos_DataPedido` com o intervalo de
  datas, lendo só os pedidos de 2026. O plano deixa de ser paralelo.
- Nas duas, `ItensPedido` é lido por `Index Scan` no índice de cobertura. Como 2026 é mais de
  um quinto dos pedidos, ler o índice inteiro custa menos do que uma busca por pedido.

### Medições

Uma execução de cada, com 200 mil pedidos. Leituras são páginas de 8 KB (`logical reads`).

| Cenário | Pedidos | ItensPedido | Total de leituras | CPU |
|---|---|---|---|---|
| Original, só com chaves primárias | 844 | 1.687 | 2.535 | 300 ms |
| Original, com os índices | 599 | 1.617 | 2.218 | 262 ms |
| Corrigida, só com chaves primárias | 844 | 1.687 | 2.549 | 61 ms |
| Corrigida, com os índices | 181 | 1.541 | 1.740 | 48 ms |
| Alternativa, com os índices | 181 | 0 | 199 | 12 ms |

O que os números dizem:

- **Só reescrever já corta o CPU em cerca de 5 vezes** (300 ms para 61 ms), mesmo sem índice
  nenhum: o banco para de calcular `YEAR()` linha a linha e de montar um plano paralelo.
- **O índice de data só ajuda depois da reescrita.** Na original ele não é usado; na corrigida,
  as leituras em `Pedidos` caem 70% (599 para 181).
- **O custo que sobra está em `ItensPedido`** (1.541 leituras), que nenhuma reescrita do filtro
  elimina.

### Alternativa: usar `Pedidos.ValorTotal`

Pela regra de negócio, `ValorTotal` já é a soma de `Quantidade x PrecoUnitario` dos itens.
Somando essa coluna, a query nem toca em `ItensPedido`, e `IX_Pedidos_DataPedido` cobre tudo:

```sql
SELECT c.Id, c.Nome, SUM(p.ValorTotal) AS Total
FROM Pedidos p
JOIN Clientes c ON c.Id = p.ClienteId
WHERE p.DataPedido >= '2026-01-01' AND p.DataPedido < '2027-01-01'
GROUP BY c.Id, c.Nome
ORDER BY Total DESC;
```

Devolve o mesmo resultado com 199 leituras, 91% a menos que a original (2.218). É o que o
endpoint `GET /relatorios/top-clientes` faz. Mantive a versão com `ItensPedido` como "a
corrigida" porque ela preserva o cálculo da query original; a alternativa depende de
`ValorTotal` estar sempre coerente com os itens, o que a aplicação garante na criação do pedido.
