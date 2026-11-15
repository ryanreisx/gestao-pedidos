# Plano de implementação — Mini Gestão de Pedidos

Base: [spec.md](spec.md) e o enunciado do teste técnico.

Regra geral: cada etapa é pequena, termina com algo verificável e vira um commit
(mensagem clara, sem squash). Nada de camadas ou abstrações além do que a spec pede.

## Decisões técnicas

| Tema | Escolha | Por quê |
|---|---|---|
| Estrutura | `database/`, `backend/`, `frontend/`, `docker-compose.yml` na raiz | Espelha as partes do enunciado |
| Backend | Um único projeto ASP.NET Core (.NET 10) com Minimal API | App pequeno; várias camadas seria excesso |
| Organização do backend | Pastas `Endpoints/`, `Dominio/`, `Dados/` | `Dominio/` fica sem dependência de banco, para ser testado em unidade |
| Acesso a dados | EF Core mapeado para o schema já existente, sem migrations | O schema pertence aos scripts em `database/` (exigência do enunciado) |
| Datas | Gravadas em UTC; conversão para `Kind=Utc` na leitura | Sem isso o JSON sai sem o `Z` que a spec mostra |
| Data do pedido | `DateTime.UtcNow` direto | Simples; os testes de período inserem pedidos com data fixa pelo banco |
| Baixa de estoque | `UPDATE ... SET Estoque = Estoque - @q WHERE Id = @id AND Estoque >= @q` na transação (`ExecuteUpdateAsync`), itens em ordem de `ProdutoId` | Garante estoque nunca negativo sem locks manuais; a ordem fixa evita deadlock entre pedidos com os mesmos produtos |
| Parâmetros de consulta | `page` e `pageSize` como `int`, datas como `DateOnly?`, na assinatura do endpoint | O framework barra formato inválido com 400; no código ficam só as checagens de faixa |
| Erros | `ProblemDetails` com a mensagem do primeiro erro encontrado; `AddProblemDetails()` para os 400 do framework | Simples: o domínio para na primeira falha |
| Testes | xUnit em dois projetos: `GestaoPedidos.UnitTests` e `GestaoPedidos.IntegrationTests` (`WebApplicationFactory` + Testcontainers) | Unitários rodam sem Docker; integração usa SQL Server real e isolado |
| Frontend | Vite + React + TypeScript, React Router, `fetch`, CSS simples | Duas telas; não precisa de biblioteca de estado nem de UI |
| Servir o front | nginx servindo o build e repassando `/api` para o backend, removendo o prefixo (o proxy do Vite faz o mesmo em desenvolvimento) | Evita CORS; as rotas da API ficam sem prefixo, como na spec |
| Inicialização do banco | Serviço `db-init` de execução única no compose, rodando os scripts de `database/` com `sqlcmd -b`; comando escrito no próprio compose | A imagem do SQL Server não executa scripts sozinha; sem arquivo `.sh` não há problema de quebra de linha do Windows |
| MCP | DBHub (`@bytebase/dbhub`) via `npx`, em `.mcp.json`, com `cmd /c` (Windows) | Roda sem instalação e fala com SQL Server; o README explica a troca para Linux/macOS |

Portas no host: SQL Server 1433, API 5080 (8080 dentro do contêiner), front 3000.
Senha do `sa`: valor de desenvolvimento com padrão no compose e no `.mcp.json` (`${SA_PASSWORD:-...}`),
para subir sem passo manual.

Ambiente conferido: .NET SDK 10.0.401, Node 24.15, Docker 29.4, Compose v5.1.

## Etapa 0 — Versionar a documentação

- `.gitignore` (.NET, Node, `.vs/`).
- `README.md` só com os títulos das seções exigidas, para preencher a cada etapa.
- Commit de `docs/spec.md` e `docs/plan.md`.

## Etapa 1 — Configuração do Claude (Parte 2)

Um commit por item.

1. **`CLAUDE.md`** — convenções do projeto:
   - arquitetura e pastas (tabela acima, resumida);
   - nomenclatura: domínio em português (`Pedido`, `ItemPedido`), sufixos técnicos em inglês
     (`PedidoRequest`, `PedidosEndpoints`); tabelas e colunas como na spec;
   - comandos de build, testes e de subir o ambiente;
   - regras: `docs/spec.md` é a fonte da verdade; schema só muda em `database/`; commits pequenos.
2. **Hook** — `.claude/settings.json` com `PostToolUse` em `Edit|Write`, chamando
   `.claude/hooks/pos-edicao.mjs` (Node, para funcionar em Windows e Linux):
   - arquivo `.cs` editado → `dotnet build --no-restore`, mostrando só erros;
   - arquivo `.ts`/`.tsx` editado → `npx tsc --noEmit` no `frontend/`;
   - falha → sai com código 2 e devolve os erros ao Claude; outros arquivos são ignorados.
3. **Skill** — `.claude/skills/novo-endpoint/SKILL.md`: passo a passo para criar um endpoint no
   padrão do projeto (request/response, validação 400 antes de 422, `ProblemDetails`, registro
   da rota, teste de integração, atualização da spec).
4. **MCP** — `.mcp.json` na raiz apontando para o SQL Server do compose (`localhost:1433`).
   Confirmar na documentação do DBHub o formato do DSN de SQL Server e o modo somente leitura.
   A conexão só é validada no fim da Etapa 2, quando o banco existir.

Pronto quando: os quatro arquivos estão versionados e o hook dispara ao editar um arquivo.

## Etapa 2 — Banco de dados (Parte 3)

1. **`database/01-schema.sql`** — cria o banco `GestaoPedidos` e as quatro tabelas, de forma
   idempotente (`IF NOT EXISTS`):
   - PKs `IDENTITY`; FKs `Pedidos.ClienteId`, `ItensPedido.PedidoId`, `ItensPedido.ProdutoId`;
   - `DECIMAL(18,2)` para valores, `DATETIME2` para datas;
   - `CHECK` de `Estoque >= 0` e `Quantidade > 0`; `UNIQUE` em `Clientes.Email`;
   - índices:
     - `Pedidos (DataPedido DESC) INCLUDE (ClienteId, ValorTotal)`;
     - `Pedidos (ClienteId, DataPedido DESC)`;
     - `ItensPedido (PedidoId, ProdutoId)` único, `INCLUDE (Quantidade, PrecoUnitario)` —
       garante a regra do produto repetido e cobre a query da Parte 4;
     - `ItensPedido (ProdutoId)`.
2. **`database/02-seed.sql`** — só insere se `Clientes` estiver vazia: ~10 clientes,
   ~10 produtos (um com estoque zero), ~30 pedidos entre 2025 e 2026 com `ValorTotal` coerente
   com os itens. Pedidos fora de 2026 e clientes sem pedido servem à Parte 4.
3. **`docker-compose.yml`** (só banco por enquanto):
   - `sqlserver`: imagem oficial 2022, healthcheck com `sqlcmd`, volume nomeado;
   - `db-init`: mesma imagem, monta `./database`, espera o healthcheck e executa os scripts
     em ordem; termina com sucesso.
4. Validar o MCP: com o banco no ar, listar tabelas e índices e contar registros pelo Claude.

Pronto quando: `docker compose up` em máquina limpa cria e popula o banco, e rodar de novo
não duplica dados.

## Etapa 3 — Backend: base

1. Criar `backend/GestaoPedidos.Api`, `backend/GestaoPedidos.UnitTests` e
   `backend/GestaoPedidos.IntegrationTests` (uma solution).
2. `Dados/`: entidades e `AppDbContext` mapeados para o schema existente; connection string
   por variável de ambiente.
3. `GET /clientes` e `GET /produtos`.
4. Infra de testes de integração: fixture que sobe SQL Server com Testcontainers, aplica
   `database/01-schema.sql` e insere dados próprios de teste. Primeiro teste: `GET /produtos`.
5. `backend/Dockerfile` (build multi-stage, imagens .NET 10) e serviço `api` no compose,
   dependendo de `db-init` concluído.

Pronto quando: `dotnet test` passa contra SQL Server real e `docker compose up` responde
`GET /produtos` em `localhost:5080`.

## Etapa 4 — POST /pedidos

1. **Domínio + testes unitários** — `Dominio/` com duas funções puras:
   - validar o request (sem `clienteId`, sem itens, quantidade ≤ 0, produto repetido) → erro 400;
   - montar o pedido a partir de cliente e produtos já carregados (cliente inexistente, produto
     inexistente, estoque insuficiente, cópia do preço, cálculo do total) → pedido ou erro 422.
   - As duas retornam o primeiro erro encontrado, nessa ordem de checagem, sem acumular.
   - Testes unitários cobrindo cada regra da spec.
2. **Endpoint** — carrega cliente e produtos, chama o domínio, abre transação, baixa o estoque
   com o `UPDATE` condicional em ordem de `ProdutoId` (zero linhas afetadas → rollback e 422),
   grava pedido e itens, responde 201 no "Formato do pedido".
3. **Testes de integração** — pedido criado e estoque baixado; cada caso de 400 e 422;
   estoque inalterado na rejeição. Sem teste de concorrência: a garantia fica no `UPDATE`
   condicional e na `CHECK` de estoque.

## Etapa 5 — GET /pedidos

1. Checagens de faixa (`page >= 1`, `pageSize` de 1 a 50, `dataInicio <= dataFim`) e conversão
   do período para intervalo em UTC com extremos inclusivos, numa função compartilhada com o
   relatório + testes unitários.
2. Endpoint com parâmetros tipados, filtros, ordenação por `DataPedido` decrescente, `total` e itens.
3. Testes de integração: filtro por cliente, por período (bordas do dia), paginação, 400.

## Etapa 6 — GET /relatorios/top-clientes

1. Endpoint: soma de `ValorTotal` e contagem de pedidos por cliente no período, top 5,
   desempate por quantidade de pedidos e nome.
2. Testes de integração: só pedidos do período, máximo de 5, desempate, sem período.

## Etapa 7 — Debug da query (Parte 4)

Feita pelo Claude e revisada pelo autor.

1. `database/debug/carga-volume.sql`: gera 200 mil pedidos com 2 itens cada (um `INSERT ... SELECT`
   para os pedidos, outro para os itens e um `UPDATE` do `ValorTotal`). Rodado à mão, fora do
   `db-init`. Com a carga padrão (~30 pedidos) a query não fica lenta e o plano não muda.
2. Com o MCP: conferir tabelas e índices e rodar a query original e a corrigida, antes e depois
   da carga de volume.
3. Comparar antes e depois com `SET STATISTICS IO` (pelo MCP, que devolve as mensagens) e com o
   plano de execução (`SET SHOWPLAN_TEXT`, por `sqlcmd` no contêiner, porque precisa de um lote
   só para ele). Sem DMVs.
4. Medir também a query sem os índices do schema, dentro de uma transação desfeita no fim, para
   separar o ganho da reescrita do ganho dos índices.
5. Salvar em `database/debug/ranking-2026.sql` a query original, a corrigida e a alternativa
   que usa `Pedidos.ValorTotal`. Problemas confirmados:
   - `WHERE` em `p.DataPedido` anula o `LEFT JOIN`;
   - `YEAR(p.DataPedido)` impede busca no índice → intervalo `>= '2026-01-01' AND < '2027-01-01'`;
   - `GROUP BY c.Nome` junta clientes homônimos → agrupar por `c.Id, c.Nome`;
   - `ORDER BY Total` crescente → `DESC`.
6. Escrever a explicação no README.

## Etapa 8 — Frontend

1. Scaffold Vite + React + TS em `frontend/`, rotas e cliente de API tipado (`api.ts`).
2. Tela de listagem: filtros (cliente, período), tabela com itens, paginação.
3. Tela de criação: cliente, linhas de produto + quantidade (mostra preço e estoque), total
   previsto, envio e exibição dos erros da API.
4. `frontend/Dockerfile` (build com Node, nginx servindo e repassando `/api`) e serviço
   `frontend` no compose.

Um commit por item.

## Etapa 9 — Verificação final do Docker (Parte 6)

Apagar volumes, `docker compose up`, criar um pedido pelo front e vê-lo na listagem.

## Etapa 10 — README e entrega

Revisão final do README, preenchido ao longo das etapas:

- Como rodar com Docker e como rodar os testes.
- Principais decisões técnicas (tabela deste plano, atualizada).
- Uso do Claude: o que funcionou e o que foi corrigido.
- Como o MCP foi usado (Etapas 2 e 7).
- Explicação da Parte 4.

## Etapa 11 — Opcional: E2E com Playwright

Um teste: criar um pedido pelo front e vê-lo na listagem, rodando contra o compose.
Só depois de tudo acima estar pronto.

## Revisão em plan mode

A primeira versão deste plano foi revisada pelo Claude em plan mode. O que mudou:

| Mudança | Por quê |
|---|---|
| Baixa de estoque em ordem de `ProdutoId` | Evitar deadlock entre pedidos simultâneos com os mesmos produtos |
| Conversão das datas para `Kind=Utc` na leitura | O JSON sairia sem o `Z` |
| Testes em dois projetos em vez de um | Unitários rodam sem Docker |
| nginx remove o prefixo `/api` | As rotas da API não têm prefixo na spec |
| `db-init` sem arquivo `.sh`, `sqlcmd -b` | Quebra de linha do Windows; falhar em erro de SQL |
| `cmd /c` no `.mcp.json` | `npx` não inicia como servidor MCP no Windows sem isso |
| Índice único de `ItensPedido` com `INCLUDE` | Cobre a query da Parte 4 |
| `README.md` criado na Etapa 0 | Preencher aos poucos, não no fim |
| Dockerfile da API na Etapa 3 e do front na Etapa 8 | `docker compose up` funciona desde a primeira etapa de código; a Etapa 9 vira só verificação |
| Carga de volume na Etapa 7 | Com ~30 pedidos não há lentidão para investigar |

## Ajustes e discordâncias

O que o autor ajustou ou recusou na revisão proposta pelo Claude:

| Proposta do Claude | Decisão do autor | Por quê |
|---|---|---|
| Receber `page`, `pageSize` e datas como texto e validar à mão, para controlar a mensagem de erro | Recusada: parâmetros com os tipos corretos, validação de formato pelo framework | Mais simples |
| Injetar `TimeProvider` para os testes controlarem a data | Recusada: `DateTime.UtcNow` direto | Mais simples |
| Ler o plano de execução pelas DMVs via MCP na Etapa 7 | Recusada: comparação manual com `SET STATISTICS IO` e plano de execução; o MCP fica para ver tabelas, índices e rodar as queries | Investigação feita pelo autor |
| Script de carga de volume com ~200 mil pedidos | Aceita, reduzida a um único `INSERT ... SELECT` | Basta para a query ficar lenta; não precisa de mais que isso |
| Tela de top clientes no front | Recusada: o front fica com listagem e criação; o relatório existe só na API | O enunciado não exige essa tela |
| Teste de integração com dois POSTs simultâneos pela última unidade | Recusada: sem teste de concorrência; o `UPDATE` condicional e a `CHECK` de estoque continuam | Mais simples; a garantia já está no banco |
| Domínio acumulando todos os erros do pedido na resposta | Recusada: retorna o primeiro erro encontrado | Mais simples |
| Etapa 7 com a comparação feita manualmente pelo autor (decisão anterior, acima) | Alterada: a etapa inteira é executada pelo Claude e revisada pelo autor. `STATISTICS IO` pelo MCP, plano de execução por `sqlcmd`; continua sem DMVs | Usar o MCP na investigação e ganhar tempo. O autor revisou o resultado e reproduziu as medições |
| API publicada na porta 8080 do host | Alterada: porta 5080 no host, 8080 só dentro do contêiner | A 8080 é comum demais; na máquina do autor já estava ocupada por outro projeto |
