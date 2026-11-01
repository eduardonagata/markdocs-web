import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Em desenvolvimento, o Vite serve o frontend e repassa /api ao backend
// (`dotnet run --project src/MarkDocs`, que também sobe este servidor pelo
// SpaProxy, na porta de Properties/launchSettings.json). Em produção, o build vai para dist/ e é servido pelo próprio
// backend (MarkDocs.WebUi).
const BACKEND = 'http://localhost:5280';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': BACKEND,
      '/swagger': BACKEND,
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
    chunkSizeWarningLimit: 1500,
    rollupOptions: {
      output: {
        manualChunks: {
          fluent: ['@fluentui/react-components', '@fluentui/react-icons'],
          pdf: ['pdfjs-dist'],
          markdown: ['react-markdown', 'remark-gfm'],
        },
      },
    },
  },
});
