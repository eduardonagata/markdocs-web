const ehMac = typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.platform);

/** O atalho que abre e fecha o assistente, como o sistema o escreve. */
export const atalho = ehMac ? '⌘J' : 'Ctrl+J';
