import { BrowserRouter, NavLink, Route, Routes } from 'react-router';
import { ListaPedidos } from './pages/ListaPedidos';
import { NovoPedido } from './pages/NovoPedido';

export function App() {
  return (
    <BrowserRouter>
      <header className="topo">
        <span className="marca">Gestão de Pedidos</span>
        <nav>
          <NavLink to="/" end>
            Pedidos
          </NavLink>
          <NavLink to="/novo">Novo pedido</NavLink>
        </nav>
      </header>
      <main>
        <ListaPedidos />
        <Routes>
          <Route path="/" element={null} />
          <Route path="/novo" element={<NovoPedido />} />
        </Routes>
      </main>
    </BrowserRouter>
  );
}
