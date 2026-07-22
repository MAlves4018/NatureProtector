import type { User } from './types';

export type UiCapability =
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
  | 'data_context.read'
  | 'help.read';

export type UiNavTarget =
  | 'demo'
  | 'overview'
  | 'dashboard'
  | 'mission'
  | 'risk'
  | 'pipeline'
  | 'simulation'
  | 'runs'
  | 'quality'
  | 'qa'
  | 'qa-tests'
  | 'evidence'
  | 'deployments'
  | 'cloud'
  | 'approvals'
  | 'users'
  | 'admin'
  | 'context'
  | 'deployment-health'
  | 'queries'
  | 'scenario-compare'
  | 'about';

export interface UiNavigationItem {
  id: UiNavTarget;
  labelKey:
    | 'nav.demo'
    | 'nav.overview'
    | 'nav.dashboard'
    | 'nav.mission'
    | 'nav.risk'
    | 'nav.pipeline'
    | 'nav.simulation'
    | 'nav.runs'
    | 'nav.quality'
    | 'nav.qa'
    | 'nav.qa-tests'
    | 'nav.evidence'
    | 'nav.deployment-health'
    | 'nav.queries'
    | 'nav.deployments'
    | 'nav.cloud'
    | 'nav.approvals'
    | 'nav.users'
    | 'nav.admin'
    | 'nav.context'
    | 'nav.scenario-compare'
    | 'nav.about';
  requiredCapability: UiCapability;
}

export const COMMON_AUTH_CAPABILITIES: readonly UiCapability[] = [
  'demo.read',
  'area.read',
  'risk.read',
  'run.read',
  'data_context.read',
  'help.read',
];

export const PUBLIC_CAPABILITIES: readonly UiCapability[] = [
  'demo.read',
  'area.read',
  'data_context.read',
  'help.read',
];

const READ_ENGINEERING: readonly UiCapability[] = [
  'quality.read',
  'evidence.read',
  'evidence.download',
  'evidence.compare',
];

export function getUiCapabilities(user: Pick<User, 'roles'> | null | undefined): Set<UiCapability> {
  const roles = user?.roles ?? [];
  if (roles.length === 0) {
    return new Set(PUBLIC_CAPABILITIES);
  }

  const recognizedRoles = new Set(['Pipeline', 'Sim', 'QA', 'Operations', 'ReleaseApprover', 'Admin']);
  if (!roles.some((role) => recognizedRoles.has(role))) {
    return new Set(['demo.read', 'help.read']);
  }

  const capabilities = new Set<UiCapability>(COMMON_AUTH_CAPABILITIES);
  const add = (items: readonly UiCapability[]) => {
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
      'simulation.execute',
    ]);
  }
  if (roles.includes('Operations')) {
    add([
      ...READ_ENGINEERING,
      'pipeline.read',
      'deployment.read',
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
      'qa.read',
      'quality.execute.static',
      'quality.execute.full',
      'evidence.execute.campaign',
      'pipeline.read',
      'scenario.read',
      'simulation.read',
      'simulation.execute',
      'deployment.read',
      'deployment.plan',
      'deployment.deploy.staging',
      'deployment.deploy.production',
      'deployment.rollback',
      'cloud.read',
      'cloud.operate.staging',
      'cloud.operate.production',
      'cloud.destroy',
      'limitations.read',
      'approval.review',
      'users.manage',
      'roles.manage',
      'admin.read',
      'admin.execute',
    ]);
  }

  return capabilities;
}

export function hasUiCapability(capabilities: Set<UiCapability>, capability: UiCapability) {
  return capabilities.has(capability);
}
