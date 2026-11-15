---
name: novo-endpoint
description: Cria um endpoint novo na API seguindo o padrão do projeto (Minimal API, domínio puro, ProblemDetails, testes). Use sempre que for adicionar ou alterar uma rota em backend/GestaoPedidos.Api.
---

# Novo endpoint

Siga os passos na ordem. Cada passo termina compilando (o hook de pós-edição avisa se quebrar).

## 1. Conferir a spec

- O endpoint precisa estar em `docs/spec.md`. Se não estiver, pare e pergunte ao autor antes
  de escrever código; se ele aprovar, atualize a spec primeiro.
- Anote da spec: rota, parâmetros, formato da resposta e os códigos de erro.

## 2. Regra de negócio em `Dominio/`

- Só se o endpoint tiver regra além de ler dados.
- Função pura: recebe dados já carregados, devolve o resultado ou o primeiro erro encontrado.
  Sem `AppDbContext`, sem tipos de HTTP.
- Escreva os testes unitários junto, em `GestaoPedidos.UnitTests`, um por regra,
  com nome `Metodo_Cenario_Resultado`.

## 3. Rota em `Endpoints/`

- Um arquivo por recurso: `Endpoints/<Recurso>Endpoints.cs`, com um método de extensão
  `Map<Recurso>Endpoints(this IEndpointRouteBuilder app)`. Rota nova de um recurso que já
  existe vai no arquivo dele.
- Tipos de request e response como `record` no mesmo arquivo, com sufixo `Request`/`Response`.
  Nunca devolva entidades do EF direto.
- Parâmetros de consulta tipados na assinatura (`int page = 1`, `DateOnly? dataInicio`).
  O framework responde 400 para formato inválido; no código, só checagens de faixa.
- Ordem dentro do handler:
  1. validação de entrada → 400;
  2. carregar dados e aplicar a regra de domínio → 422;
  3. gravar (em transação, se alterar mais de uma tabela);
  4. responder (`201` com o recurso criado, `200` nas consultas).
- Erros com `Results.Problem(detail: "<mensagem em português>", statusCode: ...)`.
- Consultas somente leitura usam `AsNoTracking()`.
- Registre o grupo em `Program.cs` se o arquivo for novo.

## 4. Teste de integração

- Em `GestaoPedidos.IntegrationTests`, usando a fixture de SQL Server existente.
- Cada teste cria os próprios dados (cliente, produtos) e não depende da carga inicial.
- Cubra: o caminho feliz, cada código de erro da spec e o efeito no banco quando houver
  (ex.: estoque baixado, estoque inalterado na rejeição).

## 5. Fechar

- `dotnet test backend` passando.
- Se o front consome o endpoint, adicione a função tipada em `frontend/src/api.ts`.
- Mostre o resultado ao autor e só faça o commit depois que ele aceitar
  (`feat: <descrição curta em português>`).
