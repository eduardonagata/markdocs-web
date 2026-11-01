import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter, Navigate, RouterProvider } from 'react-router-dom';
import { FluentProvider } from '@fluentui/react-components';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Casca } from './componentes/Casca';
import { DocumentoPagina } from './paginas/Documento';
import { Documentos } from './paginas/Documentos';
import { NovoDocumento } from './paginas/NovoDocumento';
import { Templates } from './paginas/Templates';
import { tema } from './tema/tema';
import './index.css';

const consultas = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 15_000, retry: 1, refetchOnWindowFocus: false },
  },
});

const rotas = createBrowserRouter([
  {
    path: '/',
    element: <Casca />,
    children: [
      { index: true, element: <Navigate to="/documentos" replace /> },
      { path: 'documentos', element: <Documentos /> },
      { path: 'documentos/novo', element: <NovoDocumento /> },
      { path: 'documentos/:id', element: <DocumentoPagina /> },
      { path: 'templates', element: <Templates /> },
      { path: '*', element: <Navigate to="/documentos" replace /> },
    ],
  },
]);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <FluentProvider theme={tema} style={{ height: '100%', background: 'transparent' }}>
      <QueryClientProvider client={consultas}>
        <RouterProvider router={rotas} />
      </QueryClientProvider>
    </FluentProvider>
  </StrictMode>,
);
