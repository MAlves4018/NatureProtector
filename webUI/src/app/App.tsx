import {
  ChakraProvider, createSystem, defaultConfig, defineConfig,
} from '@chakra-ui/react';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { MainPage } from './components/views/mainPage';
import { DashBoards } from './components/views/dashBoards';
import { AreaProvider } from './context/areaContext';

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
      <AreaProvider>
        <RouterProvider router={routes}/>
      </AreaProvider>
    </ChakraProvider>
  );
}