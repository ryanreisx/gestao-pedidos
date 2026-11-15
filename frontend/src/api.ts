// Cliente da API. Todas as chamadas do front passam por aqui, com o prefixo /api.

export interface Cliente {
  id: number;
  nome: string;
  email: string;
}

export interface Produto {
  id: number;
  nome: string;
  preco: number;
  estoque: number;
}

export interface ItemPedido {
  produtoId: number;
  produtoNome: string;
  quantidade: number;
  precoUnitario: number;
}

export interface Pedido {
  id: number;
  clienteId: number;
  clienteNome: string;
  dataPedido: string;
  valorTotal: number;
  itens: ItemPedido[];
}

export interface PaginaDePedidos {
  itens: Pedido[];
  page: number;
  pageSize: number;
  total: number;
}

export interface FiltroDePedidos {
  clienteId?: number;
  dataInicio?: string; // yyyy-MM-dd
  dataFim?: string; // yyyy-MM-dd
  page: number;
  pageSize: number;
}

export interface NovoPedido {
  clienteId: number;
  itens: { produtoId: number; quantidade: number }[];
}

// Erro devolvido pela API. A mensagem vem do campo "detail" do ProblemDetails.
export class ErroDaApi extends Error {
  status: number;

  constructor(status: number, mensagem: string) {
    super(mensagem);
    this.status = status;
  }
}

const MENSAGENS_AMIGAVEIS: Record<string, string> = {
  'clienteId é obrigatório.': 'Selecione um cliente.',
  'dataInicio não pode ser maior que dataFim.': 'A data inicial não pode ser maior que a data final.',
  'page deve ser maior ou igual a 1.': 'A página deve ser maior ou igual a 1.',
  'page é grande demais.': 'O número da página é grande demais.',
  'One or more validation errors occurred.': 'Os dados informados são inválidos. Verifique os campos preenchidos.',
  'Bad Request': 'Os dados informados são inválidos. Verifique os campos preenchidos.',
};

function tornarMensagemAmigavel(mensagem: string): string {
  const direta = MENSAGENS_AMIGAVEIS[mensagem];
  if (direta) return direta;

  if (/^Cliente \d+ não existe\.$/.test(mensagem)) {
    return 'O cliente selecionado não foi encontrado.';
  }
  if (/^Produto \d+ não existe\.$/.test(mensagem)) {
    return 'O produto selecionado não foi encontrado.';
  }
  const pageSize = mensagem.match(/^pageSize deve estar entre (\d+) e (\d+)\.$/);
  if (pageSize) {
    return `A quantidade por página deve estar entre ${pageSize[1]} e ${pageSize[2]}.`;
  }

  return mensagem;
}

async function requisitar<T>(caminho: string, opcoes?: RequestInit): Promise<T> {
  let resposta: Response;
  try {
    resposta = await fetch(`/api${caminho}`, opcoes);
  } catch {
    throw new ErroDaApi(0, 'Não foi possível falar com a API.');
  }

  if (!resposta.ok) {
    const problema = await resposta.json().catch(() => null);
    const bruta = problema?.detail ?? problema?.title ?? `Erro ${resposta.status}`;
    throw new ErroDaApi(resposta.status, tornarMensagemAmigavel(bruta));
  }

  return resposta.json() as Promise<T>;
}

export function listarClientes(): Promise<Cliente[]> {
  return requisitar('/clientes');
}

export function listarProdutos(): Promise<Produto[]> {
  return requisitar('/produtos');
}

export function listarPedidos(filtro: FiltroDePedidos): Promise<PaginaDePedidos> {
  const parametros = new URLSearchParams({
    page: String(filtro.page),
    pageSize: String(filtro.pageSize),
  });
  if (filtro.clienteId) parametros.set('clienteId', String(filtro.clienteId));
  if (filtro.dataInicio) parametros.set('dataInicio', filtro.dataInicio);
  if (filtro.dataFim) parametros.set('dataFim', filtro.dataFim);
  return requisitar(`/pedidos?${parametros}`);
}

export function criarPedido(pedido: NovoPedido): Promise<Pedido> {
  return requisitar('/pedidos', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(pedido),
  });
}

export function mensagemDeErro(erro: unknown): string {
  return erro instanceof Error ? erro.message : 'Erro inesperado.';
}
