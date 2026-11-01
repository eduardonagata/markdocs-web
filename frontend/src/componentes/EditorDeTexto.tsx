import { forwardRef, useImperativeHandle, useRef, useState, type ClipboardEvent, type DragEvent } from 'react';
import { makeStyles, mergeClasses, tokens } from '@fluentui/react-components';

const useEstilos = makeStyles({
  raiz: {
    height: '100%',
    display: 'flex',
    backgroundColor: tokens.colorNeutralBackground1,
  },
  area: {
    flex: 1,
    resize: 'none',
    border: 'none',
    outline: 'none',
    padding: '28px 32px',
    boxSizing: 'border-box',
    fontFamily: "ui-monospace, 'SF Mono', Menlo, Consolas, 'Liberation Mono', monospace",
    fontSize: '13.5px',
    lineHeight: '1.65',
    color: tokens.colorNeutralForeground1,
    backgroundColor: 'transparent',
    tabSize: 4,
  },
  soltando: {
    outline: `2px dashed ${tokens.colorNeutralStroke1}`,
    outlineOffset: '-10px',
    backgroundColor: tokens.colorNeutralBackground2,
  },
});

export interface EditorDeTextoRef {
  inserir: (texto: string) => void;
}

interface Props {
  valor: string;
  onMudar: (valor: string) => void;
  /** Uma imagem colada ou solta no editor: grava e devolve o trecho de Markdown a inserir. */
  onImagem: (arquivo: File) => Promise<string | null>;
}

/**
 * O Markdown como texto. Uma imagem colada ou arrastada para cá é gravada nos
 * ativos do documento, e o editor insere `![…](imagens/<nome>)` no cursor.
 */
export const EditorDeTexto = forwardRef<EditorDeTextoRef, Props>(function EditorDeTexto({ valor, onMudar, onImagem }, ref) {
  const estilos = useEstilos();
  const area = useRef<HTMLTextAreaElement>(null);
  const [soltando, setSoltando] = useState(false);

  const inserir = (texto: string) => {
    const elemento = area.current;
    if (!elemento) return;
    const inicio = elemento.selectionStart;
    const fim = elemento.selectionEnd;
    const novo = valor.slice(0, inicio) + texto + valor.slice(fim);
    onMudar(novo);
    requestAnimationFrame(() => {
      elemento.focus();
      elemento.setSelectionRange(inicio + texto.length, inicio + texto.length);
    });
  };

  useImperativeHandle(ref, () => ({ inserir }));

  const imagensDe = (lista: DataTransferItemList | FileList | null): File[] => {
    if (!lista) return [];
    const arquivos = 'length' in lista && lista instanceof FileList
      ? Array.from(lista)
      : Array.from(lista as DataTransferItemList).filter((i) => i.kind === 'file').map((i) => i.getAsFile()).filter((f): f is File => f !== null);
    return arquivos.filter((f) => f.type.startsWith('image/'));
  };

  const gravar = async (arquivos: File[]) => {
    for (const arquivo of arquivos) {
      const trecho = await onImagem(arquivo);
      if (trecho) inserir(trecho);
    }
  };

  const aoColar = (evento: ClipboardEvent<HTMLTextAreaElement>) => {
    const imagens = imagensDe(evento.clipboardData.items);
    if (imagens.length === 0) return;
    evento.preventDefault();
    void gravar(imagens);
  };

  const aoSoltar = (evento: DragEvent<HTMLTextAreaElement>) => {
    setSoltando(false);
    const imagens = imagensDe(evento.dataTransfer.files);
    if (imagens.length === 0) return;
    evento.preventDefault();
    void gravar(imagens);
  };

  return (
    <div className={mergeClasses(estilos.raiz, soltando && estilos.soltando)}>
      <textarea
        ref={area}
        className={estilos.area}
        value={valor}
        spellCheck
        lang="pt-BR"
        aria-label="Markdown do documento"
        onChange={(e) => onMudar(e.target.value)}
        onPaste={aoColar}
        onDragOver={(e) => {
          if (Array.from(e.dataTransfer.items).some((i) => i.kind === 'file')) {
            e.preventDefault();
            setSoltando(true);
          }
        }}
        onDragLeave={() => setSoltando(false)}
        onDrop={aoSoltar}
      />
    </div>
  );
});
