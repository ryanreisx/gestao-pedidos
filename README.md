# Mini Gestão de Pedidos

Aplicação de cadastro e consulta de pedidos de clientes.
Especificação em [docs/spec.md](docs/spec.md) e plano de implementação em [docs/plan.md](docs/plan.md).

## Como rodar com Docker

Suba banco, API e front com um único comando na raiz do repositório:

```bash
docker compose up --build
```

| Serviço | Porta no host | Endereço / Acesso |
|---|---|---|
| Front (React + nginx) | `3000` | http://localhost:3000 |
| API (.NET 10) | `5080` (`8080` no contêiner) | http://localhost:5080 |
| SQL Server 2022 | `1433` | `localhost:1433` (`sa` / senha padrão `GestaoPedidos123`, ou variável `SA_PASSWORD`) |

Na subida, o serviço `db-init` espera o healthcheck do SQL Server e executa `database/01-schema.sql` e `database/02-seed.sql`. Os scripts são idempotentes: criam o banco `GestaoPedidos`, as tabelas e os índices, e aplicam a carga inicial (10 clientes, 10 produtos e 30 pedidos) apenas se a tabela `Clientes` estiver vazia.

Para derrubar os contêineres e apagar o volume do banco:

```bash
docker compose down -v
```

## Como rodar os testes

### Unitários

Testam as regras de negócio puras em `Dominio/` (`CriacaoDePedido`, `Paginacao` e `Periodo`), sem banco e sem precisar do Docker:

```bash
dotnet test backend/GestaoPedidos.UnitTests
```

### Integração

Precisam do Docker no ar. A fixture (`ApiFixture`) usa `WebApplicationFactory` e `Testcontainers.MsSql` para subir um contêiner SQL Server 2022 isolado, aplicar `database/01-schema.sql` e testar os endpoints contra o banco real (cada teste cria os próprios registros e não depende da carga inicial):

```bash
dotnet test backend/GestaoPedidos.IntegrationTests
```

Para rodar unitários e integração juntos:

```bash
dotnet test backend
```

### E2E com Playwright

A Etapa 11 (opcional em [docs/plan.md](docs/plan.md)) previa um teste E2E cobrindo a criação de pedido e a listagem. Não criei uma suíte de código com `@playwright/test` no repositório; em vez disso, rodei a verificação E2E no navegador pelo Claude usando o MCP do Playwright (`@playwright/mcp` configurado em `.mcp.json`) contra o ambiente no ar (`docker compose up` em `http://localhost:3000`), abrindo a tela de novo pedido, preenchendo cliente e itens, enviando o formulário e conferindo o pedido criado na listagem.

## Principais decisões técnicas

Resumo das decisões detalhadas em [docs/plan.md](docs/plan.md):

| Tema | Escolha | Motivo |
|---|---|---|
| Estrutura | `database/`, `backend/`, `frontend/` e `docker-compose.yml` na raiz | Espelha as partes do enunciado |
| Backend | Projeto único ASP.NET Core (.NET 10) com Minimal API, separado em `Endpoints/`, `Dominio/` e `Dados/` | Aplicação pequena; várias camadas seriam excesso, e manter `Dominio/` com funções puras permite testar as regras em unidade sem banco |
| Acesso a dados | EF Core mapeado para o schema existente, sem migrations | Os scripts em `database/` são a única fonte do schema |
| Inicialização do banco | Serviço `db-init` de execução única no compose rodando `database/*.sql` com `sqlcmd -b` | A imagem do SQL Server não roda scripts sozinha; o comando direto no `docker-compose.yml` evita arquivo `.sh` e problemas de quebra de linha (`CRLF`) no Windows |
| Baixa de estoque | `UPDATE ... SET Estoque = Estoque - @q WHERE Id = @id AND Estoque >= @q` (`ExecuteUpdateAsync`) na transação, em ordem de `ProdutoId`, mais `CHECK (Estoque >= 0)` | Garante estoque nunca negativo em pedidos simultâneos sem locks manuais; atualizar em ordem fixa de `ProdutoId` evita deadlock |
| Datas | `DateTime.UtcNow` na criação e conversão para `DateTimeKind.Utc` na leitura; filtro de período convertido para intervalo UTC com extremos inclusivos | Simples e garante que as datas saiam com sufixo `Z` no JSON |
| Parâmetros e erros | Parâmetros tipados na rota (`int`, `DateOnly?`) + `AddProblemDetails()`; erros 400 antes de 422, retornando o primeiro erro em `ProblemDetails` | O próprio framework barra formato inválido com 400; no código ficam só as checagens de faixa e regras de negócio |
| Testes | xUnit dividido em `GestaoPedidos.UnitTests` e `GestaoPedidos.IntegrationTests` (`WebApplicationFactory` + Testcontainers) | Unitários rodam rápido sem Docker; integração valida queries, transações e constraints em um SQL Server real e isolado |
| Frontend e proxy | Vite + React + TypeScript, React Router, `fetch` e CSS simples; nginx em produção repassando `/api` para a API sem o prefixo (igual ao proxy do Vite) | Duas telas não justificam biblioteca de estado ou de UI; o proxy evita CORS e mantém as rotas da API iguais às da spec |

## Como usei o Claude

Usei o Claude Code ao longo de todas as etapas, apoiado na especificação em [docs/spec.md](docs/spec.md), no plano em [docs/plan.md](docs/plan.md) e na configuração do repositório:

- **Rules ([CLAUDE.md](CLAUDE.md)):** define `docs/spec.md` como fonte da verdade e `docs/plan.md` como roteiro, documenta a estrutura de pastas, a separação entre `Endpoints/`, `Dominio/` (funções puras) e `Dados/` (sem migrations), a nomenclatura (domínio em português, sufixos técnicos em inglês, testes `Metodo_Cenario_Resultado`), os comandos do projeto e a regra de commits pequenos somente após validação.
- **Hook ([.claude/settings.json](.claude/settings.json) e [.claude/hooks/pos-edicao.mjs](.claude/hooks/pos-edicao.mjs)):** hook `PostToolUse` em `Edit|Write` que roda `dotnet build --no-restore` ao editar arquivos `.cs` em `backend/` e `npx tsc --noEmit` ao editar `.ts`/`.tsx` em `frontend/`. Se falhar, sai com código `2` e devolve os erros para o Claude corrigir antes de continuar.
- **Skill ([.claude/skills/novo-endpoint/SKILL.md](.claude/skills/novo-endpoint/SKILL.md)):** roteiro passo a passo para criar endpoints no padrão do projeto (conferir a spec, criar regra pura em `Dominio/` com testes unitários, criar rota em `Endpoints/` com `record` de request/response, validar 400 antes de 422 com `ProblemDetails`, escrever testes de integração isolados na `ApiFixture` e atualizar `frontend/src/api.ts`).
- **MCPs ([.mcp.json](.mcp.json)):** dois servidores MCP configurados no projeto (ambos iniciados com `cmd /c npx` por estar no Windows; em Linux/macOS basta trocar `"command": "cmd"` e `"/c", "npx"` por `"command": "npx"`):
  - **DBHub (`sqlserver` — `@bytebase/dbhub@1.6.1`):** conectado ao banco `GestaoPedidos` em `localhost:1433`. Usei no fim da Etapa 2 para conferir tabelas, índices e contagens da carga inicial, e na Etapa 7 (Parte 4) para inspecionar os índices e rodar as queries de ranking coletando `SET STATISTICS IO ON` e `SET STATISTICS TIME ON`.
  - **Playwright (`playwright` — `@playwright/mcp@0.0.85`):** navegador isolado (`--isolated`) usado para validar o fluxo ponta a ponta no front em `http://localhost:3000` (criar um pedido na tela de novo pedido e conferir que ele aparece no topo da listagem).

### O que funcionou e o que precisei corrigir

- **Plano refeito em plan mode:** o primeiro plano foi escrito fora do plan mode; pedi para refazer em plan mode. Dessa revisão saíram ajustes importantes que entraram no projeto: baixar estoque em ordem de `ProdutoId` para evitar deadlock, marcar `DateTimeKind.Utc` na leitura do EF Core para o JSON sair com `Z`, separar testes unitários e de integração em dois projetos, rodar os scripts SQL direto no comando do `db-init` com `sqlcmd -b` (sem arquivo `.sh` para não sofrer com `CRLF` no Windows), remover o prefixo `/api` no nginx e adicionar a carga de volume na Etapa 7.
- **Propostas que recusei por complicarem:** durante a revisão do plano, recusei sugestões que adicionavam complexidade sem necessidade:
  - receber `page`, `pageSize` e datas como `string` para validar o formato manualmente (mantive `int` e `DateOnly?`, deixando o próprio framework retornar 400 para formato inválido);
  - injetar `TimeProvider` para controlar a data nos testes (mantive `DateTime.UtcNow` direto na API e, nos testes de período, inseri os pedidos com data fixa direto no banco pela fixture);
  - investigar a query da Parte 4 lendo DMVs pelo MCP (mantive a comparação simples com `SET STATISTICS IO/TIME` pelo MCP e `SET SHOWPLAN_TEXT ON` via `sqlcmd`).
  - Também recusei criar tela de top clientes no front (o enunciado não pedia), acumular lista de erros na validação (mantive retorno apenas do primeiro erro) e escrever teste de integração com chamadas simultâneas (a garantia já fica no `UPDATE` condicional com `Estoque >= @q` e na `CHECK` do banco).
- **MCP do banco na primeira sessão:** o MCP do SQL Server não conectou quando abri a primeira sessão porque o contêiner e o banco `GestaoPedidos` ainda não existiam. Depois de subir o banco na Etapa 2 com `docker compose up`, reconectei a sessão com `/mcp` e funcionou normalmente.
- **Porta 8080 ocupada:** o plano previa expor a API na porta `8080`, mas ela já estava ocupada na minha máquina. Mudei o mapeamento no `docker-compose.yml` para `5080:8080` (API em `localhost:5080` no host e `8080` apenas na rede interna dos contêineres).
- **Hook bloqueando edições no front:** durante a implementação do frontend, o hook bloqueou edições com erro de tipo, obrigando o Claude a corrigir os tipos antes de seguir. Também precisei ajustar o comando do hook de `npx tsc -b` para `npx tsc --noEmit` para bater com o `tsconfig.json` do front.
- **Dois commits intermediários do front não compilam isoladamente:** na Etapa 8, os arquivos do front foram criados e depois separados em commits (`d0218fd` com o scaffold e `main.tsx`, `ade6f59` com `ListaPedidos.tsx` e `4d89cad` com `NovoPedido.tsx` e `App.tsx`). Como `main.tsx` já importava `./App` no primeiro commit, os dois commits intermediários (`d0218fd` e `ade6f59`) não compilam se testados isoladamente — o build fecha a partir de `4d89cad`. Optei por não reescrever o histórico.
- **Validações fora da spec que aceitei e levei para a spec:** ao implementar o domínio, o Claude acrescentou duas validações que não estavam previstas em `docs/spec.md`: rejeitar com 400 item sem `produtoId` (`produtoId <= 0`) no `POST /pedidos` e rejeitar `page` grande demais (quando `(page - 1) * pageSize` estoura `int.MaxValue`) no `GET /pedidos`. Como faziam sentido, aceitei as duas e atualizei `docs/spec.md` (assim como o critério de desempate por `id` para clientes homônimos no top clientes).
- **Redesenho visual do front:** o visual das telas foi redesenhado com o Google Stitch a partir de prints das telas originais e aplicado com o Antigravity; mantive a lógica, os textos e o CSS simples, sem biblioteca de UI.

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
