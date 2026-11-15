# Mini Gestão de Pedidos

Aplicação de pedidos de clientes: SQL Server, API .NET 10 e front React + TypeScript,
tudo subindo com `docker compose up`.

## Fontes da verdade

- `docs/spec.md`: o que o sistema faz (regras, endpoints, critérios de aceite). É a fonte da
  verdade; se o código ou o plano divergirem dela, corrija a divergência antes de seguir.
- `docs/plan.md`: como e em que ordem implementar.
- Não implemente nada fora do escopo da spec. Sem camadas ou abstrações extras.

## Estrutura

```
database/    scripts SQL (schema, carga, debug) — única fonte do schema
backend/     GestaoPedidos.Api, GestaoPedidos.UnitTests, GestaoPedidos.IntegrationTests
frontend/    Vite + React + TypeScript
docs/        spec.md e plan.md
```

### Backend (`backend/GestaoPedidos.Api`)

Um único projeto ASP.NET Core com Minimal API:

- `Endpoints/`: um arquivo por recurso (`PedidosEndpoints.cs`), com as rotas e os tipos de
  request/response.
- `Dominio/`: regras de negócio em funções puras, sem banco nem HTTP. É o que os testes
  unitários cobrem.
- `Dados/`: entidades e `AppDbContext` do EF Core, mapeados para o schema já existente.

Regras:

- Sem migrations do EF. O schema só muda em `database/`.
- Validação de entrada (400) vem antes das regras de negócio (422).
- Erros sempre em `ProblemDetails`, com mensagem em português. O domínio retorna o primeiro
  erro encontrado.
- Parâmetros de consulta tipados na assinatura do endpoint (`int`, `DateOnly?`).
- Datas em UTC. Período com `dataInicio` e `dataFim` inclusivos.
- Baixa de estoque com `UPDATE` condicional (`Estoque >= quantidade`) dentro da transação,
  em ordem de `ProdutoId`.

### Frontend (`frontend/`)

- React Router, `fetch` e CSS simples. Sem biblioteca de estado ou de UI.
- Todas as chamadas à API passam por `src/api.ts`, usando o prefixo `/api`.

## Nomenclatura

- Domínio em português, como na spec: `Pedido`, `ItemPedido`, `Cliente`, `Produto`, `Estoque`.
- Sufixos técnicos em inglês: `PedidoRequest`, `PedidoResponse`, `PedidosEndpoints`, `AppDbContext`.
- Tabelas e colunas exatamente como na spec: `Clientes`, `Produtos`, `Pedidos`, `ItensPedido`.
- JSON em camelCase: `clienteId`, `valorTotal`.
- Testes: `Metodo_Cenario_Resultado`, por exemplo `CriarPedido_SemEstoque_Retorna422`.
- Scripts SQL numerados na ordem de execução: `01-schema.sql`, `02-seed.sql`.

## Comandos

```bash
docker compose up --build                             # sobe banco, API e front
docker compose down -v                                # derruba e apaga os dados

dotnet build backend                                  # build do backend
dotnet test backend/GestaoPedidos.UnitTests           # testes unitários (sem Docker)
dotnet test backend/GestaoPedidos.IntegrationTests    # integração (precisa do Docker no ar)

npm --prefix frontend run dev                         # front em desenvolvimento
npm --prefix frontend run build                       # checagem de tipos + build
```

Portas: SQL Server 1433, API 8080, front 3000.

## Ferramentas do Claude neste repositório

- Hook `.claude/hooks/pos-edicao.mjs`: depois de cada edição, compila o backend (`.cs`) ou
  checa os tipos do front (`.ts`/`.tsx`). Se falhar, corrija antes de seguir.
- Skill `novo-endpoint`: use ao criar qualquer endpoint novo.
- MCP `sqlserver` (`.mcp.json`): acesso ao banco do compose para consultar tabelas, índices e
  rodar queries. Precisa do `docker compose up` no ar.

## Commits

- Em português, curtos, no formato `tipo: descrição` (`feat`, `fix`, `test`, `docs`, `chore`).
- Um commit por pequena implementação, seguindo as etapas do `docs/plan.md`.
- Só commitar depois que o autor validar e aceitar a alteração.
- Sem squash, sem reescrever histórico.
