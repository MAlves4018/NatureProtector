import React, { Suspense, lazy, useState } from 'react';
import {
  ChakraProvider, createSystem, defaultConfig, defineConfig, Box,
} from '@chakra-ui/react';
import { createBrowserRouter, Outlet, RouterProvider } from 'react-router-dom';
import { NavBar } from './components/views/navBar';
import { getColors } from './utils/utils';
import { TokenProvider } from './context/TokenContext';

const MainPage = lazy(() => import('./components/views/mainPage').then(module => ({ default: module.MainPage })));
const DashBoards = lazy(() => import('./components/views/dashBoards').then(module => ({ default: module.DashBoards })));
const Pipeline = lazy(() => import('./components/views/Pipeline').then(module => ({ default: module.Pipeline })));
const DeveloperRuntimeControl = lazy(() => import('./components/views/DeveloperRuntimeControl').then(module => ({ default: module.DeveloperRuntimeControl })));
const Workspace = lazy(() => import('./components/views/Workspace').then(module => ({ default: module.Workspace })));
const LogInOut = lazy(() => import('./components/views/LogInOut').then(module => ({ default: module.LogInOut })));
const UiV2App = lazy(() => import('./ui-v2/UiV2App').then(module => ({ default: module.UiV2App })));

// ─── Chakra system ─────────────────────────────────────────────────────────────
const system = createSystem(defaultConfig, defineConfig({ theme: {} }));

function AppLayout({ isDark, setIsDark }: { isDark: boolean; setIsDark: React.Dispatch<React.SetStateAction<boolean>> }) {  
  return (
    <>
      <NavBar isDark={isDark} setIsDark={setIsDark} />
      <Suspense fallback={<RouteLoading isDark={isDark} />}>
        <Outlet />
      </Suspense>
    </>
  );
}

// ─── Root ──────────────────────────────────────────────────────────────────────
function RouteLoading({ isDark }: { isDark: boolean }) {
  const c = getColors(isDark);

  return (
    <Box
      minH="calc(100vh - 58px)"
      bg={c.pageBg}
      color={c.textSecond}
      display="flex"
      alignItems="center"
      justifyContent="center"
      fontWeight={700}
    >
      Loading...
    </Box>
  );
}

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
          path: "/login",
          element: <LogInOut isDark={isDark} />,
        },
        {
          path: "/ui-v2",
          element: <UiV2App isDark={isDark} />,
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
      <TokenProvider>
        <Box bg={c.pageBg} minH="100vh" display="flex" flexDirection="column">
          <RouterProvider router={routes} />
        </Box>
      </TokenProvider>
    </ChakraProvider>
  );
}
