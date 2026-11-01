import { useEffect, useState } from 'react';

const formato = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'medium', timeStyle: 'short' });

/** "há 3 min", "ontem, 14:02", "12 de out. de 2026, 09:10". */
export function quando(iso: string): string {
  const data = new Date(iso);
  const segundos = (Date.now() - data.getTime()) / 1000;
  if (segundos < 60) return 'agora mesmo';
  if (segundos < 3600) return `há ${Math.floor(segundos / 60)} min`;
  if (segundos < 6 * 3600) return `há ${Math.floor(segundos / 3600)} h`;
  return formato.format(data);
}

/** O nome do arquivo, sem extensão, para o texto alternativo de uma imagem. */
export function alternativo(nome: string): string {
  return nome.replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ');
}


/** Se a janela é estreita (celular): a barra de ferramentas passa a mostrar só ícones. */
export function useEstreito(limite = 760): boolean {
  const consulta = `(max-width: ${limite}px)`;
  const [estreito, setEstreito] = useState(() => window.matchMedia(consulta).matches);
  useEffect(() => {
    const lista = window.matchMedia(consulta);
    const mudar = () => setEstreito(lista.matches);
    lista.addEventListener('change', mudar);
    return () => lista.removeEventListener('change', mudar);
  }, [consulta]);
  return estreito;
}
