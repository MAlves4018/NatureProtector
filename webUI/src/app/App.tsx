import React, { useState, useEffect } from 'react';
import {
  ChakraProvider, createSystem, defaultConfig, defineConfig, Box,
} from '@chakra-ui/react';
import { createBrowserRouter, Outlet, RouterProvider, useLocation } from 'react-router-dom';
import { MainPage } from './components/views/mainPage';
import { DashBoards } from './components/views/dashBoards';
import { NavBar } from './components/views/navBar';
import { getColors } from './utils/utils';
import { Pipeline } from './components/views/Pipeline';
import { DeveloperRuntimeControl } from './components/views/DeveloperRuntimeControl';
import { Workspace } from './components/views/Workspace';

// ─── Chakra system ─────────────────────────────────────────────────────────────
const system = createSystem(defaultConfig, defineConfig({ theme: {} }));

function AppLayout({ isDark, setIsDark }: { isDark: boolean; setIsDark: React.Dispatch<React.SetStateAction<boolean>> }) {
  const location = useLocation();
  const hideGlobalNav = location.pathname.startsWith("/workspace/") || /^\/dashboards\/[^/]+\/?$/.test(location.pathname);

  return (
    <>
      {!hideGlobalNav && <NavBar isDark={isDark} setIsDark={setIsDark} />}
      <Outlet />
    </>
  );
}

// ─── Root ──────────────────────────────────────────────────────────────────────
export default function App() {
  const [isDark, setIsDark] = useState(false);
  const c = getColors(isDark);

  const routes = createBrowserRouter([
    {
      path: "/",
      element: <AppLayout isDark={isDark} setIsDark={setIsDark} />,
      children: [
        {
          index: true,
          element: <MainPage isDark={isDark} />,
        },
        {
          path: "/workspace/:areaCode",
          element: <Workspace isDark={isDark} setIsDark={setIsDark} />,
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
              element: <Workspace isDark={isDark} setIsDark={setIsDark} />,
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
        <RouterProvider router={routes} />
      </Box>
    </ChakraProvider>
  );
}
