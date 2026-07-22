import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api } from '../services/api';
import { useToken } from '../context/TokenContext';
import {
  getUiCapabilities,
  hasUiCapability,
  PUBLIC_CAPABILITIES,
  type UiCapability,
  type UiNavTarget,
} from '../capabilities';
import { defaultPageFor, getUiPages, type UiPageDefinition } from '../navigation/pageRegistry';

interface UiCapabilityContextValue {
  user: ReturnType<typeof useToken>['user'];
  capabilities: Set<UiCapability>;
  capabilityAuthority: string;
  capabilitiesLoading: boolean;
  capabilitiesError: Error | null;
  pages: readonly UiPageDefinition[];
  activePage: UiNavTarget;
  setActivePage: (page: UiNavTarget) => void;
  isPublic: boolean;
  isDark: boolean;
  canReadArea: boolean;
  canReadRisk: boolean;
  canReadRun: boolean;
  canReadScenario: boolean;
  canExecuteSimulation: boolean;
  canReadPipeline: boolean;
  canReadEvidence: boolean;
  canExecuteFullQa: boolean;
}

const UiCapabilityContext = createContext<UiCapabilityContextValue | null>(null);
const FAIL_CLOSED_CAPABILITIES = new Set<UiCapability>(PUBLIC_CAPABILITIES);

export function UiCapabilityProvider({ children, isDark = false }: { children: ReactNode; isDark?: boolean }) {
  const { user } = useToken();
  const [activePage, setActivePage] = useState<UiNavTarget>('demo');
  const [serverCapabilities, setServerCapabilities] = useState<Set<UiCapability> | null>(null);
  const [capabilityAuthority, setCapabilityAuthority] = useState('public-capability-policy');
  const [capabilitiesLoading, setCapabilitiesLoading] = useState(false);
  const [capabilitiesError, setCapabilitiesError] = useState<Error | null>(null);

  const publicCapabilities = useMemo(() => new Set<UiCapability>(FAIL_CLOSED_CAPABILITIES), []);
  const capabilities = useMemo(() => {
    if (!user) return publicCapabilities;
    if (serverCapabilities) return serverCapabilities;
    if (capabilitiesError) return publicCapabilities;
    return getUiCapabilities(user);
  }, [publicCapabilities, serverCapabilities, user, capabilitiesError]);
  const pages = useMemo(() => getUiPages(capabilities), [capabilities]);
  const isPublic = !user;
  const capabilityProfilePending =
    capabilitiesLoading || Boolean(user && serverCapabilities === null && !capabilitiesError);

  const canReadArea = hasUiCapability(capabilities, 'area.read');
  const canReadRisk = hasUiCapability(capabilities, 'risk.read');
  const canReadRun = hasUiCapability(capabilities, 'run.read');
  const canReadScenario = hasUiCapability(capabilities, 'scenario.read');
  const canExecuteSimulation = hasUiCapability(capabilities, 'simulation.execute');
  const canReadPipeline = hasUiCapability(capabilities, 'pipeline.read');
  const canReadEvidence = hasUiCapability(capabilities, 'evidence.read');
  const canExecuteFullQa = hasUiCapability(capabilities, 'quality.execute.full');

  const capabilityFlags = useMemo(
    () => ({
      canReadArea,
      canReadRisk,
      canReadRun,
      canReadScenario,
      canExecuteSimulation,
      canReadPipeline,
      canReadEvidence,
      canExecuteFullQa,
    }),
    [
      canReadArea,
      canReadRisk,
      canReadRun,
      canReadScenario,
      canExecuteSimulation,
      canReadPipeline,
      canReadEvidence,
      canExecuteFullQa,
    ],
  );

  useEffect(() => {
    if (!user) {
      setServerCapabilities(null);
      setCapabilityAuthority('public-capability-policy');
      setCapabilitiesLoading(false);
      setCapabilitiesError(null);
      return;
    }

    let cancelled = false;
    setServerCapabilities(null);
    setCapabilityAuthority('backend-capabilities-pending-fail-closed');
    setCapabilitiesLoading(true);
    setCapabilitiesError(null);
    api
      .getCurrentCapabilities()
      .then((profile) => {
        if (cancelled) return;
        const allowed = new Set(profile.capabilities.filter(isUiCapability));
        setServerCapabilities(allowed);
        setCapabilityAuthority(profile.authority || 'backend-role-capability-policy');
      })
      .catch((value) => {
        if (cancelled) return;
        setServerCapabilities(null);
        setCapabilityAuthority('backend-capabilities-unavailable-fail-closed');
        setCapabilitiesError(value instanceof Error ? value : new Error('Capability authority unavailable.'));
      })
      .finally(() => {
        if (!cancelled) setCapabilitiesLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [user]);

  useEffect(() => {
    const fallback = defaultPageFor(capabilities);
    if (!pages.some((page) => page.id === activePage)) {
      setActivePage(fallback);
    }
  }, [activePage, capabilities, pages]);

  const value = useMemo(
    () => ({
      user,
      capabilities,
      capabilityAuthority,
      capabilitiesLoading: capabilityProfilePending,
      capabilitiesError,
      pages,
      activePage,
      setActivePage,
      isPublic,
      isDark,
      ...capabilityFlags,
    }),
    [
      user,
      capabilities,
      capabilityAuthority,
      capabilityProfilePending,
      capabilitiesError,
      pages,
      activePage,
      isPublic,
      isDark,
      capabilityFlags,
    ],
  );

  return <UiCapabilityContext.Provider value={value}>{children}</UiCapabilityContext.Provider>;
}

export function useUiCapabilities() {
  const context = useContext(UiCapabilityContext);
  if (!context) {
    throw new Error('useUiCapabilities must be used within UiCapabilityProvider');
  }
  return context;
}

function isUiCapability(value: string): value is UiCapability {
  return getUiCapabilities({ roles: ['Pipeline', 'Sim', 'QA', 'Operations', 'ReleaseApprover', 'Admin'] }).has(
    value as UiCapability,
  );
}
