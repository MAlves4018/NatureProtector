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
  | 'quality.read'
  | 'quality.execute.static'
  | 'quality.execute.full'
  | 'evidence.read'
  | 'evidence.download'
  | 'evidence.execute.campaign'
  | 'evidence.compare'
  | 'deployment.read'
  | 'deployment.plan'
  | 'deployment.deploy.staging'
  | 'deployment.deploy.production'
  | 'deployment.rollback'
  | 'cloud.read'
  | 'cloud.operate.staging'
  | 'cloud.operate.production'
  | 'cloud.destroy'
  | 'approval.review'
  | 'users.manage'
  | 'roles.manage'
  | 'limitations.read'
  | 'admin.read'
  | 'admin.execute'
  | 'p3.read'
  | 'data_context.read'
  | 'help.read';

export type UiV2NavTarget =
  | 'demo'
  | 'mission'
  | 'risk'
  | 'pipeline'
  | 'simulation'
  | 'runs'
  | 'quality'
  | 'qa'
  | 'evidence'
  | 'deployments'
  | 'cloud'
  | 'approvals'
  | 'users'
  | 'admin'
  | 'p3'
  | 'context';

export interface UiV2NavigationItem {
  id: UiV2NavTarget;
  labelKey:
    | 'nav.demo'
    | 'nav.mission'
    | 'nav.risk'
    | 'nav.pipeline'
    | 'nav.simulation'
    | 'nav.runs'
    | 'nav.quality'
    | 'nav.qa'
    | 'nav.evidence'
    | 'nav.deployments'
    | 'nav.cloud'
    | 'nav.approvals'
    | 'nav.users'
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

const READ_ENGINEERING: readonly UiV2Capability[] = [
  'quality.read',
  'evidence.read',
  'evidence.download',
  'evidence.compare',
];

export function getUiV2Capabilities(user: Pick<User, 'roles'> | null | undefined): Set<UiV2Capability> {
  const roles = user?.roles ?? [];
  if (roles.length === 0) {
    return new Set(UI_V2_PUBLIC_CAPABILITIES);
  }

  const recognizedRoles = new Set(['Pipeline', 'Sim', 'QA', 'Operations', 'ReleaseApprover', 'Admin']);
  if (!roles.some((role) => recognizedRoles.has(role))) {
    return new Set(['demo.read', 'help.read']);
  }

  const capabilities = new Set<UiV2Capability>(UI_V2_COMMON_AUTH_CAPABILITIES);
  const add = (items: readonly UiV2Capability[]) => {
    for (const item of items) {
      capabilities.add(item);
    }
  };

  if (roles.includes('Pipeline')) {
    add(['qa.read', 'pipeline.read', 'limitations.read', ...READ_ENGINEERING]);
  }
  if (roles.includes('Sim')) {
    add(['scenario.read', 'simulation.read', 'simulation.execute', 'evidence.read']);
  }
  if (roles.includes('QA')) {
    add([
      ...READ_ENGINEERING,
      'qa.read',
      'quality.execute.static',
      'quality.execute.full',
      'evidence.execute.campaign',
    ]);
  }
  if (roles.includes('Operations')) {
    add([
      ...READ_ENGINEERING,
      'pipeline.read',
      'deployment.plan',
      'deployment.deploy.staging',
      'deployment.rollback',
      'cloud.read',
      'cloud.operate.staging',
    ]);
  }
  if (roles.includes('ReleaseApprover')) {
    add([
      ...READ_ENGINEERING,
      'deployment.plan',
      'deployment.deploy.production',
      'deployment.rollback',
      'cloud.read',
      'cloud.operate.production',
      'cloud.destroy',
      'approval.review',
    ]);
  }
  if (roles.includes('Admin')) {
    add([
      ...READ_ENGINEERING,
      'pipeline.read',
      'scenario.read',
      'simulation.read',
      'simulation.execute',
      'cloud.read',
      'users.manage',
      'roles.manage',
      'admin.read',
      'admin.execute',
      'p3.read',
    ]);
  }

  return capabilities;
}

export function hasUiV2Capability(capabilities: Set<UiV2Capability>, capability: UiV2Capability) {
  return capabilities.has(capability);
}
