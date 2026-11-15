# Mini Gestão de Pedidos

## Objetivo

Implementar uma aplicação de mini gestão de pedidos: criar pedidos de clientes com itens,
consultar pedidos com filtros e gerar um relatório dos clientes que mais compraram.

## Escopo

Dentro: criação de pedido, listagem com filtros e paginação, relatório top 5 clientes,
consulta somente leitura de clientes e produtos (para o formulário do front).

Fora: criar, editar ou excluir clientes e produtos (vêm da carga inicial), status do pedido,
detalhe de pedido por id, autenticação.

## Entidades

- Clientes: Id, Nome, Email, DataCadastro
- Produtos: Id, Nome, Preco, Estoque
- Pedidos: Id, ClienteId, DataPedido, ValorTotal
- ItensPedido: Id, PedidoId, ProdutoId, Quantidade, PrecoUnitario

Valores monetários (Preco, PrecoUnitario, ValorTotal) têm 2 casas decimais.
Estoque e Quantidade são inteiros. Datas são gravadas em UTC.

## Regras de negócio

1. O cliente precisa existir.
2. O pedido precisa ter pelo menos 1 item.
3. Cada item precisa de um produto existente e quantidade maior que zero.
4. O mesmo produto não pode aparecer em mais de um item do pedido.
5. Todos os itens precisam ter estoque suficiente. Se um falhar, o pedido inteiro é rejeitado.
6. Criar o pedido baixa o estoque, tudo na mesma transação.
7. O estoque nunca fica negativo, mesmo com pedidos simultâneos: se dois pedidos disputam
   as mesmas unidades, só um é criado e o outro é rejeitado por estoque insuficiente.
8. O PrecoUnitario é copiado do produto na criação.
9. ValorTotal é a soma de Quantidade x PrecoUnitario dos itens.
10. DataPedido é definida pelo servidor.

## Endpoints

### POST /pedidos

Body: `{ "clienteId": 1, "itens": [ { "produtoId": 2, "quantidade": 3 } ] }`

- 201: pedido criado; retorna o pedido no formato descrito em "Formato do pedido"
- 400: sem clienteId, sem itens, quantidade inválida ou produto repetido
- 422: cliente inexistente, produto inexistente ou estoque insuficiente

Os erros 400 são verificados primeiro; o 422 só ocorre com um body bem formado.

### GET /pedidos

Filtros opcionais: clienteId, dataInicio, dataFim

Paginação: page (padrão 1), pageSize (padrão 10, máximo 50)

Retorna a página e o total de registros, do mais recente para o mais antigo:
`{ "itens": [ ...pedidos ], "page": 1, "pageSize": 10, "total": 42 }`

- 400: page menor que 1, pageSize fora de 1 a 50, data em formato inválido ou dataInicio maior que dataFim

### GET /relatorios/top-clientes

Parâmetros opcionais: dataInicio, dataFim (sem período, considera todos os pedidos)

Retorna até 5 clientes com id, nome, valor total e quantidade de pedidos, do maior para o menor valor.
Em caso de empate no valor, vem primeiro quem tem mais pedidos e, depois, por ordem alfabética do nome.
Clientes sem pedido no período não aparecem.

- 400: data em formato inválido ou dataInicio maior que dataFim

### GET /clientes

Retorna todos os clientes (id, nome, email), em ordem alfabética. Sem paginação.

### GET /produtos

Retorna todos os produtos (id, nome, preco, estoque), em ordem alfabética. Sem paginação.

### Formato do pedido

Usado na resposta do POST e em cada item da listagem:

```json
{
  "id": 10,
  "clienteId": 1,
  "clienteNome": "Maria Silva",
  "dataPedido": "2026-11-15T14:30:00Z",
  "valorTotal": 150.00,
  "itens": [
    { "produtoId": 2, "produtoNome": "Teclado", "quantidade": 3, "precoUnitario": 50.00 }
  ]
}
```

### Período (dataInicio e dataFim)

- Formato `yyyy-MM-dd`.
- Os dois extremos são inclusivos: o dia inteiro de dataInicio e o dia inteiro de dataFim entram no resultado.
- Os dias são delimitados em UTC.
- Cada um pode ser informado sozinho (só a partir de, ou só até).

### Erros

Todas as respostas de erro seguem o formato ProblemDetails (RFC 7807), com mensagens em português.
A resposta traz o primeiro problema encontrado. A mensagem diz o que falhou e, quando se aplica,
qual produto, o estoque disponível e a quantidade solicitada.

## Front

Duas telas:

- Listagem de pedidos, com filtro por cliente e período e paginação.
- Criação de pedido: escolher o cliente, adicionar produtos com quantidade (mostrando preço e
  estoque) e enviar. Erros da API aparecem na tela com a mensagem recebida.

O relatório top clientes fica disponível só pela API.

## Carga inicial

Aplicada automaticamente ao subir, só quando o banco está vazio:

- Cerca de 10 clientes.
- Cerca de 10 produtos, um deles com estoque zero.
- Alguns pedidos em datas variadas, com valores coerentes com seus itens, para a listagem
  e o relatório já terem dados.

## Critérios de aceite

- Um pedido válido é criado e aparece na listagem.
- Pedido com cliente inexistente, sem itens, com produto repetido ou sem estoque é rejeitado com mensagem clara.
- Quando o pedido é rejeitado, o estoque não muda.
- Depois de um pedido criado, o estoque dos produtos diminui.
- O valor total do pedido bate com a soma dos itens.
- A listagem filtra por cliente e período e respeita a paginação.
- Paginação ou período inválidos retornam 400.
- O top clientes considera só pedidos do período e retorna no máximo 5.
- No front é possível filtrar a listagem e criar um pedido.

## Restrições técnicas

- Banco: SQL Server
- Backend: .NET 10
- Frontend: React com TypeScript
- Execução: tudo sobe com `docker compose up`, sem passos manuais
- Testes: unitários e de integração contra SQL Server real
