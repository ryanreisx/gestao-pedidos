# Plano de implementação — Mini Gestão de Pedidos

Base: [spec.md](spec.md) e o enunciado do teste técnico.

Regra geral: cada etapa é pequena, termina com algo verificável e vira um commit
(mensagem clara, sem squash). Nada de camadas ou abstrações além do que a spec pede.

## Decisões técnicas

| Tema | Escolha | Por quê |
|---|---|---|
| Estrutura | `database/`, `backend/`, `frontend/`, `docker-compose.yml` na raiz | Espelha as partes do enunciado |
| Backend | Um único projeto ASP.NET Core (.NET 10) com Minimal API | App pequeno; uma solution com várias camadas seria excesso |
| Organização do backend | Pastas `Endpoints/`, `Dominio/`, `Dados/` | `Dominio/` fica sem dependência de banco, para ser testado em unidade |
| Acesso a dados | EF Core mapeado para o schema já existente, sem migrations | O schema é dono dos scripts em `database/` (exigência do enunciado) |
| Baixa de estoque | `UPDATE ... SET Estoque = Estoque - @q WHERE Id = @id AND Estoque >= @q` dentro da transação (`ExecuteUpdateAsync`) | Garante a regra 7 (estoque nunca negativo) sem locks manuais |
| Erros | `ProblemDetails` com lista `erros` | Spec, seção "Erros" |
| Testes | xUnit; integração com `WebApplicationFactory` + Testcontainers (SQL Server) | Banco real e isolado, sem depender do compose estar no ar |
| Frontend | Vite + React + TypeScript, React Router, `fetch`, CSS simples | Três telas; não precisa de biblioteca de estado nem de UI |
| Servir o front | nginx servindo o build e repassando `/api` para o backend | Evita CORS e configuração de URL no front |
| Inicialização do banco | Serviço `db-init` de execução única no compose, rodando os scripts de `database/` com `sqlcmd` | A imagem do SQL Server não executa scripts sozinha |
| MCP | DBHub (`@bytebase/dbhub`) via `npx`, configurado em `.mcp.json` | Roda sem instalação e fala com SQL Server; confirmar o pacote na Etapa 1 |

Portas: SQL Server 1433, API 8080, front 3000.
Senha do `sa`: valor de desenvolvimento com padrão no compose (`${SA_PASSWORD:-...}`), para subir sem passo manual.

## Etapa 0 — Versionar a documentação

- `.gitignore` (.NET, Node, `.vs/`).
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
   - arquivo `.cs` editado → `dotnet build backend`;
   - arquivo `.ts`/`.tsx` editado → `npx tsc --noEmit` no `frontend/`;
   - falha → sai com código 2 e devolve os erros ao Claude; outros arquivos são ignorados.
3. **Skill** — `.claude/skills/novo-endpoint/SKILL.md`: passo a passo para criar um endpoint no
   padrão do projeto (request/response, validação 400 antes de 422, `ProblemDetails`, registro
   da rota, teste de integração, atualização da spec).
4. **MCP** — `.mcp.json` na raiz apontando para o SQL Server do compose (`localhost:1433`,
   senha por variável de ambiente com valor padrão). A conexão só é validada no fim da Etapa 2,
   quando o banco existir.

Pronto quando: os quatro arquivos estão versionados e o hook dispara ao editar um arquivo.

## Etapa 2 — Banco de dados (Parte 3)

1. **`database/01-schema.sql`** — cria o banco `GestaoPedidos` e as quatro tabelas, de forma
   idempotente (`IF NOT EXISTS`):
   - PKs `IDENTITY`; FKs `Pedidos.ClienteId`, `ItensPedido.PedidoId`, `ItensPedido.ProdutoId`;
   - `DECIMAL(18,2)` para valores, `DATETIME2` para datas;
   - `CHECK` de `Estoque >= 0` e `Quantidade > 0`; `UNIQUE` em `Clientes.Email` e em
     `ItensPedido (PedidoId, ProdutoId)`;
   - índices: `Pedidos (DataPedido DESC) INCLUDE (ClienteId, ValorTotal)`,
     `Pedidos (ClienteId, DataPedido DESC)`, `ItensPedido (ProdutoId)`.
2. **`database/02-seed.sql`** — só insere se `Clientes` estiver vazia: ~10 clientes,
   ~10 produtos (um com estoque zero), ~30 pedidos entre 2025 e 2026 com `ValorTotal` coerente
   com os itens. Pedidos fora de 2026 e clientes sem pedido servem à Parte 4.
3. **`docker-compose.yml`** (só banco por enquanto):
   - `sqlserver`: imagem oficial 2022, healthcheck com `sqlcmd`, volume nomeado;
   - `db-init`: mesma imagem, monta `./database`, espera o healthcheck e executa os scripts
     em ordem; termina com sucesso.
4. Validar o MCP: com o banco no ar, listar tabelas e contar registros pelo Claude.

Pronto quando: `docker compose up` em máquina limpa cria e popula o banco, e rodar de novo
não duplica dados.

## Etapa 3 — Backend: base

1. Criar `backend/GestaoPedidos.Api` e `backend/GestaoPedidos.Tests` (uma solution).
2. `Dados/`: entidades e `AppDbContext` mapeados para o schema existente; connection string
   por variável de ambiente.
3. `GET /clientes` e `GET /produtos`.
4. Infra de testes de integração: fixture que sobe SQL Server com Testcontainers, aplica
   `database/01-schema.sql` e insere dados próprios de teste. Primeiro teste: `GET /produtos`.

Pronto quando: `dotnet test` passa com o teste de integração contra SQL Server real.

## Etapa 4 — POST /pedidos

1. **Domínio + testes unitários** — `Dominio/` com duas funções puras:
   - validar o request (sem `clienteId`, sem itens, quantidade ≤ 0, produto repetido) → erros 400;
   - montar o pedido a partir de cliente e produtos já carregados (cliente inexistente, produto
     inexistente, estoque insuficiente, cópia do preço, cálculo do total) → pedido ou erros 422.
   - Testes unitários cobrindo cada regra da spec e o acúmulo de vários erros.
2. **Endpoint** — carrega cliente e produtos, chama o domínio, abre transação, baixa o estoque
   com o `UPDATE` condicional (zero linhas afetadas → rollback e 422), grava pedido e itens,
   responde 201 no "Formato do pedido".
3. **Testes de integração** — pedido criado e estoque baixado; cada caso de 400 e 422;
   estoque inalterado na rejeição; dois POSTs simultâneos pela última unidade (um 201, um 422,
   estoque zero).

## Etapa 5 — GET /pedidos

1. Validação de paginação e período (função de período compartilhada com o relatório:
   `yyyy-MM-dd`, extremos inclusivos em UTC, `dataInicio <= dataFim`) + testes unitários.
2. Endpoint com filtros, ordenação por `DataPedido` decrescente, `total` e itens.
3. Testes de integração: filtro por cliente, por período (bordas do dia), paginação, 400.

## Etapa 6 — GET /relatorios/top-clientes

1. Endpoint: soma de `ValorTotal` e contagem de pedidos por cliente no período, top 5,
   desempate por quantidade de pedidos e nome.
2. Testes de integração: só pedidos do período, máximo de 5, desempate, sem período.

## Etapa 7 — Debug da query (Parte 4)

1. Com o MCP, rodar a query original contra a carga e registrar resultado e plano de execução.
2. Corrigir e salvar em `database/debug/ranking-2026.sql` (original e corrigida). Pontos a confirmar:
   - `WHERE` em `p.DataPedido` anula o `LEFT JOIN`;
   - `YEAR(p.DataPedido)` impede uso de índice → trocar por intervalo
     `>= '2026-01-01' AND < '2027-01-01'`;
   - `GROUP BY c.Nome` junta clientes homônimos → agrupar por `c.Id, c.Nome`;
   - `ORDER BY Total` crescente → `DESC`;
   - índices de apoio (já previstos na Etapa 2; ajustar se o plano mostrar outra necessidade).
3. Guardar a comparação de planos antes/depois para o README.

## Etapa 8 — Frontend

1. Scaffold Vite + React + TS em `frontend/`, rotas e cliente de API tipado (`api.ts`).
2. Tela de listagem: filtros (cliente, período), tabela com itens, paginação.
3. Tela de criação: cliente, linhas de produto + quantidade (mostra preço e estoque), total
   previsto, envio e exibição dos erros da API.
4. Tela de top clientes: filtro de período e tabela.

Um commit por tela.

## Etapa 9 — Docker completo (Parte 6)

1. `backend/Dockerfile` (build multi-stage, imagens .NET 10).
2. `frontend/Dockerfile` (build com Node, nginx servindo e repassando `/api`).
3. Completar o compose: `api` depende de `db-init` concluído; `frontend` depende de `api`.
4. Teste final: apagar volumes, `docker compose up`, criar um pedido pelo front.

## Etapa 10 — README e entrega

- Como rodar com Docker e como rodar os testes.
- Principais decisões técnicas (tabela deste plano, atualizada).
- Uso do Claude: o que funcionou e o que foi corrigido (anotar ao longo das etapas, não no fim).
- Como o MCP foi usado (Etapas 2 e 7).
- Explicação da Parte 4.

## Etapa 11 — Opcional: E2E com Playwright

Um teste: criar um pedido pelo front e vê-lo na listagem, rodando contra o compose.
Só depois de tudo acima estar pronto.

## Ajustes e discordâncias

> Seção exigida pelo enunciado: o que foi ajustado ou recusado no plano gerado pelo Claude, e por quê.
> A preencher pelo autor ao revisar este plano e durante a implementação.

- _(pendente)_
