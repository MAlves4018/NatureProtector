import type { TechnicalLabel } from '../types';
import type { UiLocale } from '../i18n';
import { localize } from '../types';

export const TECHNICAL_LABELS: readonly TechnicalLabel[] = [
  {
    code: 'Expired',
    label: { 'pt-PT': 'Dados expirados', en: 'Expired data' },
    detail: {
      'pt-PT': 'O dado ultrapassou a janela temporal esperada.',
      en: 'The data is outside the expected time window.',
    },
  },
  {
    code: 'LowCoverage',
    label: { 'pt-PT': 'Cobertura reduzida', en: 'Low coverage' },
    detail: { 'pt-PT': 'A cobertura de leituras e células é limitada.', en: 'Reading and cell coverage is limited.' },
  },
  {
    code: 'CompleteWithCandidateDefaults',
    label: { 'pt-PT': 'Dados completos com parametros candidatos', en: 'Complete with candidate defaults' },
    detail: {
      'pt-PT': 'O protótipo aplicou parâmetros V1 candidatos.',
      en: 'The prototype applied candidate V1 parameters.',
    },
  },
  {
    code: 'ExpiredCarryForward',
    label: { 'pt-PT': 'Dados anteriores mantidos, mas expirados', en: 'Expired carried-forward data' },
    detail: {
      'pt-PT': 'Foi mantido um valor anterior; não deve ser tratado como atual.',
      en: 'A previous value was retained and should not be treated as current.',
    },
  },
  {
    code: 'NotAvailable',
    label: { 'pt-PT': 'Não disponível', en: 'Not available' },
    detail: {
      'pt-PT': 'O contrato atual nao forneceu este valor.',
      en: 'The current contract did not provide this value.',
    },
  },
];

export const DEGRADATION_PROFILE_OPTIONS = [
  'none',
  'missing-readings',
  'noise',
  'bias',
  'drift',
  'stuck-value',
  'outlier',
  'clipping/range',
  'lag/delay',
  'duplicate',
  'out-of-order',
] as const;

export function technicalLabel(code: string | null | undefined, locale: UiLocale) {
  if (!code) {
    return localize(locale, TECHNICAL_LABELS.find((item) => item.code === 'NotAvailable')!.label);
  }

  const match = TECHNICAL_LABELS.find((item) => item.code.toLowerCase() === code.toLowerCase());
  return match ? localize(locale, match.label) : code;
}

export function technicalLabelDetail(code: string, locale: UiLocale) {
  const match = TECHNICAL_LABELS.find((item) => item.code.toLowerCase() === code.toLowerCase());
  return match ? localize(locale, match.detail) : '';
}
