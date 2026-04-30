import {
  ChakraProvider, createSystem, defaultConfig, defineConfig,
} from '@chakra-ui/react';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { MainPage } from './components/views/mainPage';
import { DashBoards } from './components/views/dashBoards';

// ─── Chakra system ─────────────────────────────────────────────────────────────
const system = createSystem(defaultConfig, defineConfig({ theme: {} }));

// ─── Root ──────────────────────────────────────────────────────────────────────
export default function App() {

  const routes = createBrowserRouter([
      {
        path: "/",
        children: [
          {
            index: true,
            element: <MainPage/>,
          },
          {
            path: "/dashboards/:areaId",
            element: <DashBoards/>,
          }
        ]
      },
    ])

  return (
    <ChakraProvider value={system}>
        <RouterProvider router={routes}/>
    </ChakraProvider>
  );
}