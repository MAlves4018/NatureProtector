import type { BetaCapabilityLink } from '../types';

export const BETA_CAPABILITIES: readonly BetaCapabilityLink[] = [
  {
    id: 'monitoring',
    label: { 'pt-PT': 'Monitoring e overview operacional', en: 'Monitoring and operational overview' },
    description: {
      'pt-PT': 'Funcionalidade legacy identificada; a rota beta antiga nao esta montada na UI atual.',
      en: 'Legacy capability identified; the old beta route is not mounted in the current UI.',
    },
    href: () => null,
    status: 'legacy-not-yet-ported',
  },
  {
    id: 'map',
    label: { 'pt-PT': 'Mapa e celulas', en: 'Map and cells' },
    description: {
      'pt-PT': 'Visualizacao legacy identificada; nao ha rota beta ativa equivalente na UI atual.',
      en: 'Legacy view identified; no equivalent active beta route exists in the current UI.',
    },
    href: () => null,
    status: 'legacy-not-yet-ported',
  },
  {
    id: 'runtime-monitor',
    label: { 'pt-PT': 'Runtime monitor', en: 'Runtime monitor' },
    description: {
      'pt-PT': 'Monitor legacy identificado; usar a pagina Pipeline atual ate haver migracao explicita.',
      en: 'Legacy monitor identified; use the current Pipeline page until an explicit migration exists.',
    },
    href: () => null,
    status: 'legacy-not-yet-ported',
  },
  {
    id: 'scenario-lab',
    label: { 'pt-PT': 'Scenario Lab e Run Orchestrator', en: 'Scenario Lab and Run Orchestrator' },
    description: {
      'pt-PT': 'Fluxo legacy identificado; a UI atual usa a pagina Simulacao por roles/capabilities.',
      en: 'Legacy workflow identified; the current UI uses the Simulation page by roles/capabilities.',
    },
    href: () => null,
    status: 'legacy-not-yet-ported',
  },
  {
    id: 'evidence-comparison',
    label: { 'pt-PT': 'Evidence & Comparison', en: 'Evidence & Comparison' },
    description: {
      'pt-PT': 'Comparacao legacy identificada; a UI atual separa qualidade e Evidence Explorer.',
      en: 'Legacy comparison identified; the current UI separates quality and Evidence Explorer.',
    },
    href: () => null,
    status: 'legacy-not-yet-ported',
  },
  {
    id: 'flow-model',
    label: { 'pt-PT': 'Flow Explorer e Model & Provenance', en: 'Flow Explorer and Model & Provenance' },
    description: {
      'pt-PT': 'Detalhe legacy identificado; nao foi portado como rota ativa nesta UI.',
      en: 'Legacy detail identified; it has not been ported as an active route in this UI.',
    },
    href: () => null,
    status: 'legacy-not-yet-ported',
  },
];
