import { createLightTheme, type BrandVariants, type Theme } from '@fluentui/react-components';

/**
 * Grafite sobre papel: a ênfase é peso, não matiz. A cor de marca é um cinza
 * quente escuro, e o único objeto que deve chamar atenção é o documento.
 */
const grafite: BrandVariants = {
  10: '#050404',
  20: '#0f0d0c',
  30: '#171413',
  40: '#1c1917',
  50: '#211e1c',
  60: '#262220',
  70: '#292524',
  80: '#33302d',
  90: '#44403c',
  100: '#57534e',
  110: '#78716c',
  120: '#a8a29e',
  130: '#c4bfba',
  140: '#d6d3d1',
  150: '#e7e5e4',
  160: '#f5f5f4',
};

const base = createLightTheme(grafite);

export const tema: Theme = {
  ...base,
  colorNeutralBackground1: '#ffffff',
  colorNeutralBackground2: '#fafaf9',
  colorNeutralBackground3: '#f5f5f4',
  colorNeutralBackground4: '#f0efed',
  colorNeutralForeground1: '#1c1917',
  colorNeutralForeground2: '#44403c',
  colorNeutralForeground3: '#78716c',
  colorNeutralStroke1: '#e7e5e4',
  colorNeutralStroke2: '#efeeec',
  colorBrandForegroundLink: '#1c1917',
  colorBrandForegroundLinkHover: '#000000',
  borderRadiusMedium: '6px',
  borderRadiusLarge: '10px',
};
