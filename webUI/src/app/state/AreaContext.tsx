import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api } from '../services/api';
import { useUiCapabilities } from './CapabilityContext';
import { useUiLocale } from './LocaleContext';
import type { AreaResponse } from '../types';
import { resolveUiArea, type UiAreaResolutionModel } from '../coreContext';

const AREA_STORAGE_KEY = 'np.Ui.areaCode';

interface UiAreaContextValue {
  areas: AreaResponse[];
  areasLoading: boolean;
  areaError: Error | null;
  selectedAreaCode: string;
  setSelectedAreaCode: (areaCode: string) => void;
  areaResolution: UiAreaResolutionModel;
  resolvedAreaCode: string | null;
  reloadAreaContext: () => void;
}

const UiAreaContext = createContext<UiAreaContextValue | null>(null);

export function UiAreaProvider({ children }: { children: ReactNode }) {
  const { canReadArea } = useUiCapabilities();
  const { locale } = useUiLocale();
  const [areas, setAreas] = useState<AreaResponse[]>([]);
  const [areasLoading, setAreasLoading] = useState(false);
  const [areaError, setAreaError] = useState<Error | null>(null);
  const [selectedAreaCode, setSelectedAreaCode] = useState(
    () => initialValueFromQuery('area') ?? sessionStorage.getItem(AREA_STORAGE_KEY) ?? '',
  );
  const [refreshNonce, setRefreshNonce] = useState(0);

  const areaResolution = useMemo(
    () => resolveUiArea(selectedAreaCode, areas, locale, areasLoading, areaError),
    [selectedAreaCode, areas, locale, areasLoading, areaError],
  );
  const resolvedAreaCode = areaResolution.resolvedArea?.code ?? null;

  const reloadAreaContext = useCallback(() => setRefreshNonce((value) => value + 1), []);

  useEffect(() => {
    if (selectedAreaCode) {
      sessionStorage.setItem(AREA_STORAGE_KEY, selectedAreaCode);
      setQueryParam('area', selectedAreaCode);
    } else {
      sessionStorage.removeItem(AREA_STORAGE_KEY);
      setQueryParam('area', null);
    }
  }, [selectedAreaCode]);

  useEffect(() => {
    void refreshNonce;

    if (!canReadArea) {
      setAreas([]);
      return;
    }

    let cancelled = false;
    setAreasLoading(true);
    setAreaError(null);
    api
      .getAreas()
      .then((result) => {
        if (!cancelled) setAreas(result);
      })
      .catch((err) => {
        if (!cancelled) setAreaError(asError(err, 'Failed to load areas'));
      })
      .finally(() => {
        if (!cancelled) setAreasLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [canReadArea, refreshNonce]);

  const value = useMemo(
    () => ({
      areas,
      areasLoading,
      areaError,
      selectedAreaCode,
      setSelectedAreaCode,
      areaResolution,
      resolvedAreaCode,
      reloadAreaContext,
    }),
    [areas, areasLoading, areaError, selectedAreaCode, areaResolution, resolvedAreaCode, reloadAreaContext],
  );

  return <UiAreaContext.Provider value={value}>{children}</UiAreaContext.Provider>;
}

export function useUiArea() {
  const context = useContext(UiAreaContext);
  if (!context) {
    throw new Error('useUiArea must be used within UiAreaProvider');
  }
  return context;
}

function initialValueFromQuery(name: string) {
  return new URLSearchParams(window.location.search).get(name);
}

function setQueryParam(name: string, value: string | null) {
  const url = new URL(window.location.href);
  if (value) {
    url.searchParams.set(name, value);
  } else {
    url.searchParams.delete(name);
  }
  window.history.replaceState(null, '', `${url.pathname}${url.search}${url.hash}`);
}

function asError(value: unknown, fallback: string) {
  return value instanceof Error ? value : new Error(fallback);
}
