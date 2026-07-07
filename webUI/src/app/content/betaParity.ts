import type { BetaCapabilityLink } from '../types';

export const BETA_CAPABILITIES: readonly BetaCapabilityLink[] = [
  {
    id: 'monitoring',
    label: { 'pt-PT': 'Monitoring e overview operacional', en: 'Monitoring and operational overview' },
    description: {
      'pt-PT': 'Continua disponivel na interface beta enquanto a v2 foca leitura orientada por tarefas.',
      en: 'Remains available in beta while v2 focuses on task-based reading.',
    },
    href: (areaCode) => (areaCode ? `/workspace/${areaCode}` : null),
    status: 'available-in-beta',
  },
  {
    id: 'map',
    label: { 'pt-PT': 'Mapa e celulas', en: 'Map and cells' },
    description: {
      'pt-PT': 'A visualizacao geografica completa permanece na beta.',
      en: 'The full geographic view remains in beta.',
    },
    href: (areaCode) => (areaCode ? `/dashboards/${areaCode}/dashNMap` : null),
    status: 'available-in-beta',
  },
  {
    id: 'runtime-monitor',
    label: { 'pt-PT': 'Runtime monitor', en: 'Runtime monitor' },
    description: {
      'pt-PT': 'Monitor tecnico detalhado continua na rota beta de pipeline.',
      en: 'Detailed technical monitor remains in the beta pipeline route.',
    },
    href: (areaCode) => (areaCode ? `/dashboards/${areaCode}/pipeline` : null),
    status: 'available-in-beta',
  },
  {
    id: 'scenario-lab',
    label: { 'pt-PT': 'Scenario Lab e Run Orchestrator', en: 'Scenario Lab and Run Orchestrator' },
    description: {
      'pt-PT': 'A v2 tem simulacao simplificada; fluxos extensos permanecem temporariamente na beta.',
      en: 'v2 has simplified simulation; extended workflows remain temporarily in beta.',
    },
    href: (areaCode) => (areaCode ? `/workspace/${areaCode}` : null),
    status: 'available-in-beta',
  },
  {
    id: 'evidence-comparison',
    label: { 'pt-PT': 'Evidence & Comparison', en: 'Evidence & Comparison' },
    description: {
      'pt-PT': 'A v2 separa qualidade/evidencia; comparacoes completas continuam na beta.',
      en: 'v2 separates quality/evidence; full comparisons remain in beta.',
    },
    href: (areaCode) => (areaCode ? `/workspace/${areaCode}` : null),
    status: 'available-in-beta',
  },
  {
    id: 'flow-model',
    label: { 'pt-PT': 'Flow Explorer e Model & Provenance', en: 'Flow Explorer and Model & Provenance' },
    description: {
      'pt-PT': 'Detalhe profundo de fluxo/modelo permanece na beta ate decisao de migracao.',
      en: 'Deep flow/model detail remains in beta until migration decision.',
    },
    href: (areaCode) => (areaCode ? `/workspace/${areaCode}` : null),
    status: 'available-in-beta',
  },
];
