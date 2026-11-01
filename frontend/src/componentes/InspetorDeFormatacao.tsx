import { useEffect, useMemo, useRef, useState } from 'react';
import { Dropdown, Field, makeStyles, Option, Slider, Switch, Text, tokens } from '@fluentui/react-components';
import type { Ajuste, Eixo, ValorDeEixo } from '../api/tipos';

const useEstilos = makeStyles({
  grupo: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    paddingBottom: tokens.spacingVerticalXL,
    marginBottom: tokens.spacingVerticalL,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    ':last-child': { borderBottom: 'none' },
  },
  tituloDoGrupo: {
    textTransform: 'uppercase',
    letterSpacing: '0.06em',
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase100,
    fontWeight: tokens.fontWeightSemibold,
  },
  medida: {
    display: 'grid',
    gridTemplateColumns: '1fr 64px',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  numero: {
    textAlign: 'right',
    fontVariantNumeric: 'tabular-nums',
    color: tokens.colorNeutralForeground2,
  },
  cor: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  amostraDeCor: {
    width: '32px',
    height: '24px',
    padding: 0,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusSmall,
    backgroundColor: 'transparent',
    cursor: 'pointer',
  },
});

interface Props {
  eixos: Eixo[];
  /** Um ajuste por vez, já com o valor final do controle. */
  onAjuste: (ajuste: Ajuste) => void;
}

/**
 * O inspetor é função dos eixos: um controle por tipo de domínio, agrupados
 * como o pacote os declara. A tela não sabe o que é um "título 2" nem uma
 * "margem" — família nova chega com eixos novos e o inspetor já os mostra.
 */
export function InspetorDeFormatacao({ eixos, onAjuste }: Props) {
  const estilos = useEstilos();
  const grupos = useMemo(() => {
    const mapa = new Map<string, Eixo[]>();
    for (const eixo of eixos) mapa.set(eixo.grupo, [...(mapa.get(eixo.grupo) ?? []), eixo]);
    return [...mapa.entries()];
  }, [eixos]);

  return (
    <div>
      {grupos.map(([grupo, lista]) => (
        <section key={grupo} className={estilos.grupo}>
          <Text className={estilos.tituloDoGrupo}>{grupo}</Text>
          {lista.map((eixo) => (
            <Controle key={eixo.id} eixo={eixo} onAjuste={onAjuste} />
          ))}
        </section>
      ))}
    </div>
  );
}

function formatar(numero: number) {
  return Number.isInteger(numero) ? String(numero) : numero.toFixed(2).replace(/\.?0+$/, '');
}

function Controle({ eixo, onAjuste }: { eixo: Eixo; onAjuste: (ajuste: Ajuste) => void }) {
  const estilos = useEstilos();
  const [local, setLocal] = useState<ValorDeEixo>(eixo.valor);
  const espera = useRef<number>();

  // O valor que o servidor devolveu prevalece — inclusive aparado ao domínio.
  useEffect(() => setLocal(eixo.valor), [eixo.valor]);
  useEffect(() => () => window.clearTimeout(espera.current), []);

  const mudar = (valor: ValorDeEixo, imediato = false) => {
    setLocal(valor);
    window.clearTimeout(espera.current);
    const enviar = () => onAjuste({ eixo: eixo.id, valor });
    if (imediato) enviar();
    else espera.current = window.setTimeout(enviar, 350);
  };

  const { dominio } = eixo;
  switch (dominio.tipo) {
    case 'medida': {
      const atual = local.tipo === 'medida' ? local : { tipo: 'medida' as const, valor: dominio.min, unidade: dominio.unidade };
      const noDominio = Math.min(Math.max(atual.valor, dominio.min), dominio.max);
      return (
        <Field label={eixo.rotulo} size="small">
          <div className={estilos.medida}>
            <Slider
              size="small"
              min={dominio.min}
              max={dominio.max}
              step={dominio.passo}
              value={atual.unidade === dominio.unidade ? noDominio : dominio.min}
              onChange={(_, d) => mudar({ tipo: 'medida', valor: d.value, unidade: dominio.unidade })}
            />
            <Text size={200} className={estilos.numero}>
              {formatar(atual.valor)} {atual.unidade}
            </Text>
          </div>
        </Field>
      );
    }
    case 'escolha': {
      const id = local.tipo === 'escolha' ? local.id : '';
      const rotulo = dominio.opcoes.find((o) => o.id === id)?.rotulo ?? id;
      return (
        <Field label={eixo.rotulo} size="small">
          <Dropdown
            size="small"
            value={rotulo}
            selectedOptions={[id]}
            onOptionSelect={(_, d) => d.optionValue && mudar({ tipo: 'escolha', id: d.optionValue }, true)}
          >
            {dominio.opcoes.map((o) => (
              <Option key={o.id} value={o.id}>
                {o.rotulo}
              </Option>
            ))}
          </Dropdown>
        </Field>
      );
    }
    case 'familiaTipografica': {
      const familia = local.tipo === 'familiaTipografica' ? local.familia : '';
      return (
        <Field label={eixo.rotulo} size="small">
          <Dropdown
            size="small"
            value={familia}
            selectedOptions={[familia]}
            onOptionSelect={(_, d) => d.optionValue && mudar({ tipo: 'familiaTipografica', familia: d.optionValue }, true)}
          >
            {dominio.disponiveis.map((f) => (
              <Option key={f} value={f}>
                {f}
              </Option>
            ))}
          </Dropdown>
        </Field>
      );
    }
    case 'alternancia':
      return (
        <Switch
          label={eixo.rotulo}
          checked={local.tipo === 'alternancia' && local.ligado}
          onChange={(_, d) => mudar({ tipo: 'alternancia', ligado: d.checked }, true)}
        />
      );
    case 'cor': {
      const hexadecimal = local.tipo === 'cor' ? local.hexadecimal : '#000000';
      return (
        <Field label={eixo.rotulo} size="small">
          <div className={estilos.cor}>
            <input
              type="color"
              className={estilos.amostraDeCor}
              value={/^#[0-9a-f]{6}$/i.test(hexadecimal) ? hexadecimal : '#000000'}
              onChange={(e) => mudar({ tipo: 'cor', hexadecimal: e.target.value })}
              aria-label={eixo.rotulo}
            />
            <Text size={200} className={estilos.numero}>
              {hexadecimal}
            </Text>
          </div>
        </Field>
      );
    }
  }
}
