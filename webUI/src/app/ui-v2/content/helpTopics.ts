import type { HelpTopic, HelpTopicId } from '../types';

export const HELP_TOPICS: readonly HelpTopic[] = [
  topic('overview', 'Visao geral', 'Overview', 'Mostra o estado do prototipo e os proximos passos seguros.', 'Shows prototype status and safe next steps.', 'A pagina nao substitui a beta nem declara operacao real.', 'This page does not replace beta or claim real operation.'),
  topic('risk', 'Risco', 'Risk', 'Resultado calculado pelo prototipo quando existem dados elegiveis.', 'Prototype-calculated output when eligible data exists.', 'Nao e alerta oficial nem calibracao cientifica final.', 'It is not an official alert or final scientific calibration.'),
  topic('origin', 'Origem dos dados', 'Data origin', 'Identifica se o valor vem do catalogo, runtime ou projecao persistida.', 'Identifies whether a value comes from catalog, runtime or persisted projection.', 'Origem nao implica validacao externa.', 'Origin does not imply external validation.'),
  topic('freshness', 'Atualidade', 'Freshness', 'Indica se o dado parece atual, parcial ou desatualizado.', 'Indicates whether data appears current, partial or stale.', 'Depende dos campos expostos pelo contrato atual.', 'Depends on fields exposed by the current contract.'),
  topic('coverage', 'Cobertura', 'Coverage', 'Resume cobertura de sensores, celulas ou leituras quando disponivel.', 'Summarizes sensor, cell or reading coverage when available.', 'Cobertura baixa nao deve ser escondida.', 'Low coverage must not be hidden.'),
  topic('eligibility', 'Elegibilidade', 'Eligibility', 'Explica se ha condicoes suficientes para apresentar assessment.', 'Explains whether conditions are sufficient to present an assessment.', 'Blocked nao e score zero.', 'Blocked is not score zero.'),
  topic('provenance', 'Proveniencia', 'Provenance', 'Mostra a origem metodologica ou tecnica de uma decisao apresentada.', 'Shows the methodological or technical origin of a displayed decision.', 'Nao transforma parametros candidatos em validacao cientifica.', 'Does not turn candidate parameters into scientific validation.'),
  topic('requestedResolved', 'Requested/resolved', 'Requested/resolved', 'Compara o pedido submetido com a configuracao resolvida pelo backend.', 'Compares the submitted request with backend-resolved configuration.', 'O backend continua a ser a fonte de verdade.', 'The backend remains the source of truth.'),
  topic('degradationProfile', 'Perfil de degradacao', 'Degradation profile', 'Perfil controlado usado para simular degradacao de observacoes.', 'Controlled profile used to simulate observation degradation.', 'Nao representa falha operacional real.', 'It does not represent a real operational failure.'),
  topic('runState', 'Estado da run', 'Run state', 'Mostra o estado persistido da run selecionada.', 'Shows persisted state for the selected run.', 'Estados antigos podem ficar desatualizados.', 'Old states may become stale.'),
  topic('pipeline', 'Pipeline', 'Pipeline', 'Resume ingestao, tentativas, rejeicoes e quarentena quando expostos.', 'Summarizes ingestion, attempts, rejections and quarantine when exposed.', 'Nao cria metricas novas.', 'Does not create new metrics.'),
  topic('qa', 'Qualidade', 'Quality', 'Distingue testes, execucao e evidencia de qualidade.', 'Distinguishes tests, execution and quality evidence.', 'Resultados historicos podem estar desatualizados.', 'Historical results may be stale.'),
  topic('evidence', 'Evidencia', 'Evidence', 'Mostra artefactos e claims suportadas por dados existentes.', 'Shows artifacts and claims supported by existing data.', 'Paths internos ficam em detalhes tecnicos.', 'Internal paths stay in technical details.'),
  topic('p3', 'P3 experimental', 'Experimental P3', 'Contexto experimental separado do runtime principal.', 'Experimental context separated from the main runtime.', 'Nao integrado em scoring, alertas, schema ou runtime.', 'Not integrated into scoring, alerts, schema or runtime.'),
];

function topic(
  id: HelpTopicId,
  ptTitle: string,
  enTitle: string,
  ptSummary: string,
  enSummary: string,
  ptLimitation: string,
  enLimitation: string,
): HelpTopic {
  return {
    id,
    title: { 'pt-PT': ptTitle, en: enTitle },
    summary: { 'pt-PT': ptSummary, en: enSummary },
    explanation: { 'pt-PT': ptSummary, en: enSummary },
    source: { 'pt-PT': 'Interface UI v2 sobre contratos existentes.', en: 'UI v2 over existing contracts.' },
    limitation: { 'pt-PT': ptLimitation, en: enLimitation },
    profiles: ['Public', 'Pipeline', 'Sim', 'Admin'],
  };
}

export function findHelpTopic(id: HelpTopicId) {
  return HELP_TOPICS.find(topic => topic.id === id) ?? HELP_TOPICS[0];
}
