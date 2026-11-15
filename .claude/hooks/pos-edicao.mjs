// Hook PostToolUse (Edit|Write): valida o projeto afetado pelo arquivo editado.
//   .cs         -> dotnet build do backend
//   .ts / .tsx  -> checagem de tipos do frontend
// Sai com código 2 e os erros em stderr para o Claude corrigir antes de seguir.

import { execSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, extname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const raiz = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');

function arquivoEditado() {
  try {
    const entrada = JSON.parse(readFileSync(0, 'utf8'));
    return entrada.tool_input?.file_path ?? '';
  } catch {
    return '';
  }
}

function executar(comando, pasta) {
  try {
    execSync(comando, { cwd: pasta, stdio: 'pipe', encoding: 'utf8' });
    return null;
  } catch (erro) {
    return `${erro.stdout ?? ''}${erro.stderr ?? ''}`.trim() || erro.message;
  }
}

function falhar(titulo, saida) {
  process.stderr.write(`${titulo}\n${saida}\n`);
  process.exit(2);
}

const arquivo = arquivoEditado();
if (!arquivo) process.exit(0);

const caminho = relative(raiz, resolve(raiz, arquivo)).replaceAll('\\', '/');
const extensao = extname(caminho).toLowerCase();

const backend = join(raiz, 'backend');
if (extensao === '.cs' && caminho.startsWith('backend/') && existsSync(backend)) {
  const opcoes = '-nologo -v q -clp:ErrorsOnly';
  let erro = executar(`dotnet build --no-restore ${opcoes}`, backend);
  // Projeto ainda não restaurado (ou pacote novo): tenta de novo com restore.
  if (erro && /NETSDK1004|NU1\d{3}/.test(erro)) {
    erro = executar(`dotnet build ${opcoes}`, backend);
  }
  if (erro) falhar('dotnet build falhou:', erro);
}

if (['.ts', '.tsx'].includes(extensao) && caminho.startsWith('frontend/')) {
  const frontend = join(raiz, 'frontend');
  if (existsSync(join(frontend, 'node_modules'))) {
    const erro = executar('npx tsc --noEmit', frontend);
    if (erro) falhar('Checagem de tipos do frontend falhou:', erro);
  }
}
