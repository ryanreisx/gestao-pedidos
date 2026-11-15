const formatoDeMoeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

// As datas da API são UTC e os filtros de período também, então a tela mostra em UTC.
const formatoDeDataHora = new Intl.DateTimeFormat('pt-BR', {
  dateStyle: 'short',
  timeStyle: 'short',
  timeZone: 'UTC',
});

export function moeda(valor: number): string {
  return formatoDeMoeda.format(valor);
}

export function dataHoraUtc(iso: string): string {
  return formatoDeDataHora.format(new Date(iso));
}
