import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

type To = string | { pathname?: string; search?: string };
type NavigateOptions = { replace?: boolean };
type NavigateFunction = (to: To, options?: NavigateOptions) => void;

interface LocationSnapshot {
  pathname: string;
  search: string;
}

interface RouteObject {
  path?: string;
  index?: boolean;
  element: ReactNode;
  children?: RouteObject[];
}

interface Router {
  routes: RouteObject[];
}

interface RouterContextValue {
  location: LocationSnapshot;
  navigate: NavigateFunction;
}

const RouterContext = createContext<RouterContextValue | null>(null);
const OutletContext = createContext<ReactNode>(null);
const NO_MATCH = Symbol('NO_MATCH');

export function createBrowserRouter(routes: RouteObject[]): Router {
  return { routes };
}

export function RouterProvider({ router }: { router: Router }) {
  const [location, setLocation] = useState<LocationSnapshot>(() => currentBrowserLocation());

  useEffect(() => {
    const handlePopState = () => setLocation(currentBrowserLocation());
    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, []);

  const navigate = useMemo<NavigateFunction>(
    () => (to, options) => {
      const next = resolveTarget(to, location);
      const url = `${next.pathname}${next.search}`;
      options?.replace ? window.history.replaceState(null, '', url) : window.history.pushState(null, '', url);
      setLocation(next);
    },
    [location],
  );
  const content = renderRoutes(router.routes, location.pathname, '/');

  return (
    <RouterContext.Provider value={{ location, navigate }}>
      {content === NO_MATCH ? null : (content as ReactNode)}
    </RouterContext.Provider>
  );
}

export function MemoryRouter({ initialEntries = ['/'], children }: { initialEntries?: string[]; children: ReactNode }) {
  const [location, setLocation] = useState<LocationSnapshot>(() => parseUrl(initialEntries[0] ?? '/'));
  const navigate = useMemo<NavigateFunction>(
    () => (to) => {
      setLocation((current) => resolveTarget(to, current));
    },
    [],
  );

  return <RouterContext.Provider value={{ location, navigate }}>{children}</RouterContext.Provider>;
}

export function Outlet() {
  return <>{useContext(OutletContext)}</>;
}

export function Navigate({ to, replace = false }: { to: To; replace?: boolean }) {
  const navigate = useNavigate();
  useEffect(() => {
    navigate(to, { replace });
  }, [navigate, replace, to]);
  return null;
}

export function useLocation(): LocationSnapshot {
  return useRouterContext().location;
}

export function useNavigate(): NavigateFunction {
  return useRouterContext().navigate;
}

export function useSearchParams(): [
  URLSearchParams,
  (
    nextInit: URLSearchParams | string | Record<string, string> | ((current: URLSearchParams) => URLSearchParams),
    options?: NavigateOptions,
  ) => void,
] {
  const { location, navigate } = useRouterContext();
  const params = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const setSearchParams = (
    nextInit: URLSearchParams | string | Record<string, string> | ((current: URLSearchParams) => URLSearchParams),
    options?: NavigateOptions,
  ) => {
    const nextValue =
      typeof nextInit === 'function' ? nextInit(new URLSearchParams(location.search)) : new URLSearchParams(nextInit);
    const query = nextValue.toString();
    navigate({ pathname: location.pathname, search: query ? `?${query}` : '' }, options);
  };

  return [params, setSearchParams];
}

function useRouterContext() {
  const context = useContext(RouterContext);
  if (!context) {
    throw new Error('Router context is not available.');
  }
  return context;
}

function renderRoutes(routes: RouteObject[], pathname: string, basePath: string): ReactNode | typeof NO_MATCH {
  const normalizedPath = normalizePath(pathname);
  let wildcard: RouteObject | undefined;

  for (const route of routes) {
    if (route.path === '*') {
      wildcard = route;
      continue;
    }

    if (route.path === undefined && !route.index) {
      const child = route.children ? renderRoutes(route.children, normalizedPath, basePath) : null;
      if (child !== NO_MATCH) return renderElement(route, child);
      continue;
    }

    if (route.index) {
      if (normalizedPath === normalizePath(basePath)) return renderElement(route, null);
      continue;
    }

    const fullPath = resolveRoutePath(basePath, route.path ?? '');
    const exactMatch = normalizedPath === fullPath;
    const prefixMatch = fullPath === '/' ? normalizedPath.startsWith('/') : normalizedPath.startsWith(`${fullPath}/`);

    if (route.children?.length && (exactMatch || prefixMatch)) {
      const child = renderRoutes(route.children, normalizedPath, fullPath);
      if (child !== NO_MATCH) return renderElement(route, child);
    }

    if (exactMatch) return renderElement(route, null);
  }

  return wildcard ? renderElement(wildcard, null) : NO_MATCH;
}

function renderElement(route: RouteObject, outlet: ReactNode) {
  return <OutletContext.Provider value={outlet}>{route.element}</OutletContext.Provider>;
}

function currentBrowserLocation(): LocationSnapshot {
  return { pathname: window.location.pathname || '/', search: window.location.search || '' };
}

function parseUrl(value: string): LocationSnapshot {
  const url = new URL(value, 'http://localhost');
  return { pathname: normalizePath(url.pathname), search: url.search };
}

function resolveTarget(to: To, current: LocationSnapshot): LocationSnapshot {
  if (typeof to === 'string') return parseUrl(to);
  return {
    pathname: normalizePath(to.pathname ?? current.pathname),
    search: normalizeSearch(to.search ?? ''),
  };
}

function resolveRoutePath(basePath: string, path: string) {
  if (path.startsWith('/')) return normalizePath(path);
  const prefix = basePath === '/' ? '' : basePath;
  return normalizePath(`${prefix}/${path}`);
}

function normalizePath(pathname: string) {
  if (!pathname) return '/';
  const withSlash = pathname.startsWith('/') ? pathname : `/${pathname}`;
  return withSlash.length > 1 ? withSlash.replace(/\/+$/, '') : withSlash;
}

function normalizeSearch(search: string) {
  if (!search) return '';
  return search.startsWith('?') ? search : `?${search}`;
}
