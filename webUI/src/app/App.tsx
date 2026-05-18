import React, { useState, useEffect } from 'react';
import {
  ChakraProvider, createSystem, defaultConfig, defineConfig, Box,
} from '@chakra-ui/react';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { MainPage } from './components/views/mainPage';
import { DashBoards } from './components/views/dashBoards';
import { NavBar } from './components/views/navBar';
import { getColors } from './utils/utils';
import { DashMain } from './components/views/DashMain';
import { Pipeline } from './components/views/Pipeline';
import { DeveloperRuntimeControl } from './components/views/DeveloperRuntimeControl';

// ─── Chakra system ─────────────────────────────────────────────────────────────
const system = createSystem(defaultConfig, defineConfig({ theme: {} }));

// ─── Root ──────────────────────────────────────────────────────────────────────
export default function App() {
  const [isDark, setIsDark] = useState(false);
  const c = getColors(isDark);

  const routes = createBrowserRouter([
    {
      path: "/",
      children: [
        {
          index: true,
          element: <MainPage isDark={isDark} />,
        },
        {
          path: "/dev/runtime",
          element: <DeveloperRuntimeControl isDark={isDark} />,
        },
        {
          path: "/dashboards/:areaCode",
          children: [
            {
              index: true,
              element: <DashMain isDark={isDark} />,
            },
            {
              path: "/dashboards/:areaCode/dashNMap",
              element: <DashBoards isDark={isDark} />,
            },
            {
              path: "/dashboards/:areaCode/pipeline",
              element: <Pipeline isDark={isDark} />,
            }
          ],
        }
      ]
    },
  ])

  return (
    <ChakraProvider value={system}>
      <Box bg={c.pageBg} minH="100vh" display="flex" flexDirection="column">
        <NavBar isDark={isDark} setIsDark={setIsDark} />
        <RouterProvider router={routes} />
      </Box>
    </ChakraProvider>
  );
}
