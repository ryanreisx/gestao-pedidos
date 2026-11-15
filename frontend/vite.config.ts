import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Em desenvolvimento, /api é repassado para a API do docker compose (porta 5080),
// sem o prefixo. Em produção quem faz isso é o nginx (nginx.conf).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        rewrite: (caminho) => caminho.replace(/^\/api/, ''),
      },
    },
  },
});
