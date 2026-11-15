import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import {
  criarPedido,
  listarClientes,
  listarProdutos,
  mensagemDeErro,
  type Cliente,
  type Produto,
} from '../api';
import { moeda } from '../formato';

interface LinhaDeItem {
  chave: number;
  produtoId: string;
  quantidade: string;
}

let proximaChave = 1;
const novaLinha = (): LinhaDeItem => ({ chave: proximaChave++, produtoId: '', quantidade: '1' });

export function NovoPedido() {
  const navegar = useNavigate();

  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [produtos, setProdutos] = useState<Produto[]>([]);
  const [clienteId, setClienteId] = useState('');
  const [linhas, setLinhas] = useState<LinhaDeItem[]>(() => [novaLinha()]);
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  useEffect(() => {
    Promise.all([listarClientes(), listarProdutos()])
      .then(([listaDeClientes, listaDeProdutos]) => {
        setClientes(listaDeClientes);
        setProdutos(listaDeProdutos);
      })
      .catch((falha) => setErro(mensagemDeErro(falha)));
  }, []);

  const produtoDaLinha = (linha: LinhaDeItem) => produtos.find((p) => p.id === Number(linha.produtoId));

  const totalPrevisto = linhas.reduce((soma, linha) => {
    const produto = produtoDaLinha(linha);
    return produto ? soma + produto.preco * (Number(linha.quantidade) || 0) : soma;
  }, 0);

  // Qualquer mudança no formulário apaga o erro do envio anterior.
  function alterarLinhas(novasLinhas: LinhaDeItem[]) {
    setErro(null);
    setLinhas(novasLinhas);
  }

  function alterarLinha(chave: number, mudanca: Partial<LinhaDeItem>) {
    alterarLinhas(linhas.map((linha) => (linha.chave === chave ? { ...linha, ...mudanca } : linha)));
  }

  // As regras do pedido são validadas pela API; a tela só mostra a mensagem que ela devolve.
  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);
    try {
      const pedido = await criarPedido({
        clienteId: Number(clienteId),
        itens: linhas.map((linha) => ({
          produtoId: Number(linha.produtoId),
          quantidade: Number(linha.quantidade),
        })),
      });
      navegar('/', { state: { mensagem: `Pedido #${pedido.id} criado: ${moeda(pedido.valorTotal)}.` } });
    } catch (falha) {
      setErro(mensagemDeErro(falha));
      setEnviando(false);
    }
  }

  return (
    <section>
      <h1>Novo pedido</h1>

      <form className="pedido" onSubmit={enviar}>
        <div className="campo">
          <label htmlFor="pedido-cliente">Cliente</label>
          <select
            id="pedido-cliente"
            value={clienteId}
            onChange={(e) => {
              setErro(null);
              setClienteId(e.target.value);
            }}
          >
            <option value="">Selecione…</option>
            {clientes.map((cliente) => (
              <option key={cliente.id} value={cliente.id}>
                {cliente.nome} ({cliente.email})
              </option>
            ))}
          </select>
        </div>

        <fieldset>
          <legend>Itens</legend>

          {linhas.length === 0 && <p className="vazio">Nenhum item.</p>}

          {linhas.map((linha, indice) => {
            const produto = produtoDaLinha(linha);
            return (
              <div className="linha-item" key={linha.chave}>
                <div className="campo">
                  <label htmlFor={`produto-${linha.chave}`}>Produto do item {indice + 1}</label>
                  <select
                    id={`produto-${linha.chave}`}
                    value={linha.produtoId}
                    onChange={(e) => alterarLinha(linha.chave, { produtoId: e.target.value })}
                  >
                    <option value="">Selecione…</option>
                    {produtos.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.nome} · {moeda(p.preco)} · estoque {p.estoque}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="campo">
                  <label htmlFor={`quantidade-${linha.chave}`}>Quantidade do item {indice + 1}</label>
                  <input
                    id={`quantidade-${linha.chave}`}
                    type="number"
                    value={linha.quantidade}
                    onChange={(e) => alterarLinha(linha.chave, { quantidade: e.target.value })}
                  />
                </div>
                <span className="subtotal">
                  {produto ? moeda(produto.preco * (Number(linha.quantidade) || 0)) : '—'}
                </span>
                <button
                  type="button"
                  className="secundario"
                  aria-label={`Remover item ${indice + 1}`}
                  onClick={() => alterarLinhas(linhas.filter((l) => l.chave !== linha.chave))}
                >
                  Remover
                </button>
              </div>
            );
          })}

          <button type="button" className="secundario" onClick={() => alterarLinhas([...linhas, novaLinha()])}>
            Adicionar item
          </button>
        </fieldset>

        <p className="total">
          Total previsto: <strong>{moeda(totalPrevisto)}</strong>
        </p>

        {erro && (
          <p className="aviso erro" role="alert">
            {erro}
          </p>
        )}

        <button type="submit" disabled={enviando}>
          {enviando ? 'Enviando…' : 'Criar pedido'}
        </button>
      </form>
    </section>
  );
}
