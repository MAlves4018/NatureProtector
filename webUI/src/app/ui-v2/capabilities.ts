import type { User } from '../types';

export type UiV2Capability =
  | 'demo.read'
  | 'area.read'
  | 'risk.read'
  | 'pipeline.read'
  | 'run.read'
  | 'scenario.read'
  | 'simulation.read'
  | 'simulation.execute'
  | 'qa.read'
  | 'evidence.read'
  | 'limitations.read'
  | 'admin.read'
  | 'admin.execute'
  | 'p3.read'
  | 'data_context.read'
  | 'help.read';

export type UiV2NavTarget = 'demo' | 'risk' | 'pipeline' | 'simulation' | 'runs' | 'qa' | 'evidence' | 'admin' | 'p3' | 'context';

export interface UiV2NavigationItem {
  id: UiV2NavTarget;
  labelKey:
    | 'nav.demo'
    | 'nav.risk'
    | 'nav.pipeline'
    | 'nav.simulation'
    | 'nav.runs'
    | 'nav.qa'
    | 'nav.evidence'
    | 'nav.admin'
    | 'nav.p3'
    | 'nav.context';
  requiredCapability: UiV2Capability;
}

export const UI_V2_COMMON_AUTH_CAPABILITIES: readonly UiV2Capability[] = [
  'demo.read',
  'area.read',
  'risk.read',
  'run.read',
  'data_context.read',
  'help.read',
];

export const UI_V2_PUBLIC_CAPABILITIES: readonly UiV2Capability[] = [
  'demo.read',
  'area.read',
  'data_context.read',
  'help.read',
];

export const UI_V2_PIPELINE_CAPABILITIES: readonly UiV2Capability[] = [
  ...UI_V2_COMMON_AUTH_CAPABILITIES,
  'qa.read',
  'evidence.read',
  'limitations.read',
  'pipeline.read',
];

export const UI_V2_SIM_CAPABILITIES: readonly UiV2Capability[] = [
  ...UI_V2_COMMON_AUTH_CAPABILITIES,
  'scenario.read',
  'simulation.read',
  'simulation.execute',
];

export const UI_V2_EXECUTE_CAPABILITIES: readonly UiV2Capability[] = [
  ...UI_V2_COMMON_AUTH_CAPABILITIES,
  'pipeline.read',
  'scenario.read',
  'simulation.read',
  'qa.read',
  'evidence.read',
  'limitations.read',
  'p3.read',
  'simulation.execute',
];

export function getUiV2Capabilities(user: Pick<User, 'roles'> | null | undefined): Set<UiV2Capability> {
  const roles = user?.roles ?? [];

  if (roles.length === 0) {
    return new Set(UI_V2_PUBLIC_CAPABILITIES);
  }

  if (roles.includes('Admin')) {
    return new Set([...UI_V2_EXECUTE_CAPABILITIES, 'admin.read', 'admin.execute']);
  }

  if (roles.includes('Sim')) {
    return new Set(UI_V2_SIM_CAPABILITIES);
  }

  if (roles.includes('Pipeline')) {
    return new Set(UI_V2_PIPELINE_CAPABILITIES);
  }

  return new Set(['demo.read', 'help.read']);
}

export function hasUiV2Capability(capabilities: Set<UiV2Capability>, capability: UiV2Capability) {
  return capabilities.has(capability);
}
