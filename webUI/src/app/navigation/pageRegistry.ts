import type { UiCapability, UiNavTarget } from '../capabilities';
import type { HelpTopicId } from '../types';

export interface UiPageDefinition {
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
    | 'nav.p3'
    | 'nav.context'
    | 'nav.scenario-compare'
    | 'nav.about';
  requiredCapabilities: readonly UiCapability[];
  audience: 'public' | 'common' | 'sim' | 'pipeline' | 'qa' | 'operations' | 'approver' | 'admin' | 'about';
  order: number;
  group: 'public' | 'operate' | 'technical' | 'release' | 'admin' | 'simulate' | 'about';
  helpTopic: HelpTopicId;
}

export const UI_PAGE_REGISTRY: readonly UiPageDefinition[] = [
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
    id: 'dashboard',
    labelKey: 'nav.dashboard',
    requiredCapabilities: ['area.read'],
    audience: 'public',
    order: 15,
    group: 'public',
    helpTopic: 'overview',
  },
  {
    id: 'overview',
    labelKey: 'nav.overview',
    requiredCapabilities: ['quality.read'],
    audience: 'common',
    order: 22,
    group: 'operate',
    helpTopic: 'overview',
  },
  {
    id: 'about',
    labelKey: 'nav.about',
    requiredCapabilities: ['demo.read'],
    audience: 'about',
    order: 18,
    group: 'about',
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
    group: 'simulate',
    helpTopic: 'runState',
  },
  {
    id: 'simulation',
    labelKey: 'nav.simulation',
    requiredCapabilities: ['simulation.read'],
    audience: 'sim',
    order: 50,
    group: 'simulate',
    helpTopic: 'degradationProfile',
  },
  {
    id: 'scenario-compare',
    labelKey: 'nav.scenario-compare',
    requiredCapabilities: ['run.read'],
    audience: 'common',
    order: 55,
    group: 'simulate',
    helpTopic: 'runState',
  },
  {
    id: 'queries',
    labelKey: 'nav.queries',
    requiredCapabilities: ['simulation.execute'],
    audience: 'sim',
    order: 57,
    group: 'simulate',
    helpTopic: 'pipeline',
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
    order: 63,
    group: 'technical',
    helpTopic: 'qa',
  },
  {
    id: 'qa',
    labelKey: 'nav.qa',
    requiredCapabilities: ['qa.read'],
    audience: 'qa',
    order: 65,
    group: 'technical',
    helpTopic: 'qa',
  },
  {
    id: 'qa-tests',
    labelKey: 'nav.qa-tests',
    requiredCapabilities: ['qa.read'],
    audience: 'qa',
    order: 67,
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
    id: 'deployment-health',
    labelKey: 'nav.deployment-health',
    requiredCapabilities: ['deployment.read'],
    audience: 'operations',
    order: 77,
    group: 'release',
    helpTopic: 'pipeline',
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

export function getUiPages(capabilities: Set<UiCapability>) {
  return UI_PAGE_REGISTRY.filter((page) =>
    page.requiredCapabilities.every((capability) => capabilities.has(capability)),
  ).sort((a, b) => a.order - b.order);
}

export function defaultPageFor(capabilities: Set<UiCapability>): UiNavTarget {
  if (capabilities.has('quality.read')) {
    return 'overview';
  }
  if (capabilities.has('risk.read')) {
    return 'risk';
  }
  return 'demo';
}

export function findUiPageDefinition(page: string) {
  return UI_PAGE_REGISTRY.find((definition) => definition.id === page);
}
