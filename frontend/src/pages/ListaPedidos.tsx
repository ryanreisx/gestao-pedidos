import { useEffect, useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router';
import {
  listarClientes,
  listarPedidos,
  mensagemDeErro,
  type Cliente,
  type PaginaDePedidos,
} from '../api';
import { dataHoraUtc, moeda } from '../formato';

const TAMANHO_DA_PAGINA = 10;

interface Filtros {
  clienteId: string;
  dataInicio: string;
  dataFim: string;
}

const SEM_FILTROS: Filtros = { clienteId: '', dataInicio: '', dataFim: '' };

export function ListaPedidos() {
  const location = useLocation();
  const navegar = useNavigate();

  // Mensagem deixada pela tela de criação depois de um pedido criado. Aparece uma vez:
  // é copiada para o estado da tela e apagada do histórico, para não voltar ao recarregar.
  const [mensagemDeSucesso, setMensagemDeSucesso] = useState<string | null>(
    () => (location.state as { mensagem?: string } | null)?.mensagem ?? null,
  );

  useEffect(() => {
    if (location.state) navegar(location.pathname, { replace: true, state: null });
  }, [location, navegar]);

  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [formulario, setFormulario] = useState<Filtros>(SEM_FILTROS);
  const [filtros, setFiltros] = useState<Filtros>(SEM_FILTROS); // filtros já aplicados
  const [page, setPage] = useState(1);
  const [pagina, setPagina] = useState<PaginaDePedidos | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);

  useEffect(() => {
    listarClientes()
      .then(setClientes)
      .catch(() => setClientes([]));
  }, []);

  useEffect(() => {
    let cancelado = false;
    setCarregando(true);
    setErro(null);

    listarPedidos({
      clienteId: filtros.clienteId ? Number(filtros.clienteId) : undefined,
      dataInicio: filtros.dataInicio || undefined,
      dataFim: filtros.dataFim || undefined,
      page,
      pageSize: TAMANHO_DA_PAGINA,
    })
      .then((resultado) => {
        if (!cancelado) setPagina(resultado);
      })
      .catch((falha) => {
        if (cancelado) return;
        setPagina(null);
        setErro(mensagemDeErro(falha));
      })
      .finally(() => {
        if (!cancelado) setCarregando(false);
      });

    return () => {
      cancelado = true;
    };
  }, [filtros, page]);

  function aplicarFiltros(evento: FormEvent) {
    evento.preventDefault();
    setMensagemDeSucesso(null);
    setPage(1);
    setFiltros({ ...formulario });
  }

  function limparFiltros() {
    setMensagemDeSucesso(null);
    setFormulario(SEM_FILTROS);
    setPage(1);
    setFiltros(SEM_FILTROS);
  }

  const totalDePaginas = pagina ? Math.max(1, Math.ceil(pagina.total / pagina.pageSize)) : 1;

  return (
    <section>
      <h1>Pedidos</h1>

      {mensagemDeSucesso && (
        <p className="aviso sucesso" role="status">
          {mensagemDeSucesso}
        </p>
      )}

      <form className="filtros" onSubmit={aplicarFiltros}>
        <div className="campo">
          <label htmlFor="filtro-cliente">Cliente</label>
          <select
            id="filtro-cliente"
            value={formulario.clienteId}
            onChange={(e) => setFormulario({ ...formulario, clienteId: e.target.value })}
          >
            <option value="">Todos</option>
            {clientes.map((cliente) => (
              <option key={cliente.id} value={cliente.id}>
                {cliente.nome} ({cliente.email})
              </option>
            ))}
          </select>
        </div>
        <div className="campo">
          <label htmlFor="filtro-data-inicio">Data inicial</label>
          <input
            id="filtro-data-inicio"
            type="date"
            value={formulario.dataInicio}
            onChange={(e) => setFormulario({ ...formulario, dataInicio: e.target.value })}
          />
        </div>
        <div className="campo">
          <label htmlFor="filtro-data-fim">Data final</label>
          <input
            id="filtro-data-fim"
            type="date"
            value={formulario.dataFim}
            onChange={(e) => setFormulario({ ...formulario, dataFim: e.target.value })}
          />
        </div>
        <button type="submit">Filtrar</button>
        <button type="button" className="secundario" onClick={limparFiltros}>
          Limpar
        </button>
      </form>

      {erro && (
        <p className="aviso erro" role="alert">
          {erro}
        </p>
      )}

      {pagina && (
        <>
          <table>
            <thead>
              <tr>
                <th>Pedido</th>
                <th>Cliente</th>
                <th>Data (UTC)</th>
                <th>Itens</th>
                <th className="numero">Valor total</th>
              </tr>
            </thead>
            <tbody>
              {pagina.itens.length === 0 && (
                <tr>
                  <td colSpan={5} className="vazio">
                    Nenhum pedido encontrado.
                  </td>
                </tr>
              )}
              {pagina.itens.map((pedido) => (
                <tr key={pedido.id}>
                  <td>#{pedido.id}</td>
                  <td>{pedido.clienteNome}</td>
                  <td>{dataHoraUtc(pedido.dataPedido)}</td>
                  <td>
                    <ul className="itens">
                      {pedido.itens.map((item) => (
                        <li key={item.produtoId}>
                          {item.quantidade} × {item.produtoNome} ({moeda(item.precoUnitario)})
                        </li>
                      ))}
                    </ul>
                  </td>
                  <td className="numero">{moeda(pedido.valorTotal)}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <nav className="paginacao" aria-label="Paginação">
            <button
              type="button"
              className="secundario"
              disabled={carregando || page <= 1}
              onClick={() => setPage(page - 1)}
            >
              Anterior
            </button>
            <span>
              Página {pagina.page} de {totalDePaginas} · {pagina.total}{' '}
              {pagina.total === 1 ? 'pedido' : 'pedidos'}
            </span>
            <button
              type="button"
              className="secundario"
              disabled={carregando || page >= totalDePaginas}
              onClick={() => setPage(page + 1)}
            >
              Próxima
            </button>
          </nav>
        </>
      )}

      {carregando && !pagina && <p>Carregando…</p>}
    </section>
  );
}
