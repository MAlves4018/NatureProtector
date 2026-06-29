import type { UiV2Capability, UiV2NavTarget } from '../capabilities';
import type { HelpTopicId } from '../types';

export interface UiV2PageDefinition {
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
  requiredCapabilities: readonly UiV2Capability[];
  audience: 'public' | 'common' | 'sim' | 'pipeline' | 'qa' | 'operations' | 'approver' | 'admin';
  order: number;
  group: 'public' | 'operate' | 'technical' | 'release' | 'admin';
  helpTopic: HelpTopicId;
}

export const UI_V2_PAGE_REGISTRY: readonly UiV2PageDefinition[] = [
  {
    id: 'demo',
    labelKey: 'nav.demo',
    requiredCapabilities: ['demo.read'],
    audience: 'public',
    order: 10,
    group: 'public',
    helpTopic: 'overview',
  },
  {
    id: 'context',
    labelKey: 'nav.context',
    requiredCapabilities: ['data_context.read'],
    audience: 'public',
    order: 20,
    group: 'public',
    helpTopic: 'origin',
  },
  {
    id: 'mission',
    labelKey: 'nav.mission',
    requiredCapabilities: ['quality.read'],
    audience: 'common',
    order: 25,
    group: 'operate',
    helpTopic: 'qa',
  },
  {
    id: 'risk',
    labelKey: 'nav.risk',
    requiredCapabilities: ['risk.read'],
    audience: 'common',
    order: 30,
    group: 'operate',
    helpTopic: 'risk',
  },
  {
    id: 'runs',
    labelKey: 'nav.runs',
    requiredCapabilities: ['run.read'],
    audience: 'common',
    order: 40,
    group: 'operate',
    helpTopic: 'runState',
  },
  {
    id: 'simulation',
    labelKey: 'nav.simulation',
    requiredCapabilities: ['simulation.read'],
    audience: 'sim',
    order: 50,
    group: 'operate',
    helpTopic: 'degradationProfile',
  },
  {
    id: 'pipeline',
    labelKey: 'nav.pipeline',
    requiredCapabilities: ['pipeline.read'],
    audience: 'pipeline',
    order: 60,
    group: 'technical',
    helpTopic: 'pipeline',
  },
  {
    id: 'quality',
    labelKey: 'nav.quality',
    requiredCapabilities: ['quality.read'],
    audience: 'qa',
    order: 65,
    group: 'technical',
    helpTopic: 'qa',
  },
  {
    id: 'evidence',
    labelKey: 'nav.evidence',
    requiredCapabilities: ['evidence.read'],
    audience: 'qa',
    order: 70,
    group: 'technical',
    helpTopic: 'evidence',
  },
  {
    id: 'deployments',
    labelKey: 'nav.deployments',
    requiredCapabilities: ['deployment.read'],
    audience: 'operations',
    order: 75,
    group: 'release',
    helpTopic: 'requestedResolved',
  },
  {
    id: 'cloud',
    labelKey: 'nav.cloud',
    requiredCapabilities: ['cloud.read'],
    audience: 'operations',
    order: 80,
    group: 'release',
    helpTopic: 'pipeline',
  },
  {
    id: 'approvals',
    labelKey: 'nav.approvals',
    requiredCapabilities: ['approval.review'],
    audience: 'approver',
    order: 85,
    group: 'release',
    helpTopic: 'requestedResolved',
  },
  {
    id: 'users',
    labelKey: 'nav.users',
    requiredCapabilities: ['users.manage', 'roles.manage'],
    audience: 'admin',
    order: 90,
    group: 'admin',
    helpTopic: 'requestedResolved',
  },
  {
    id: 'admin',
    labelKey: 'nav.admin',
    requiredCapabilities: ['admin.read'],
    audience: 'admin',
    order: 95,
    group: 'admin',
    helpTopic: 'requestedResolved',
  },
  {
    id: 'p3',
    labelKey: 'nav.p3',
    requiredCapabilities: ['p3.read'],
    audience: 'admin',
    order: 100,
    group: 'admin',
    helpTopic: 'p3',
  },
];

export function getUiV2Pages(capabilities: Set<UiV2Capability>) {
  return UI_V2_PAGE_REGISTRY.filter((page) =>
    page.requiredCapabilities.every((capability) => capabilities.has(capability)),
  ).sort((a, b) => a.order - b.order);
}

export function defaultPageFor(capabilities: Set<UiV2Capability>): UiV2NavTarget {
  if (capabilities.has('quality.read')) {
    return 'mission';
  }
  if (capabilities.has('risk.read')) {
    return 'risk';
  }
  return 'demo';
}
