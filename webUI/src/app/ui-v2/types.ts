import type { UiV2Locale, UiV2MessageKey } from './i18n';

export type Copy = (key: UiV2MessageKey) => string;

export type HelpTopicId =
  | 'overview'
  | 'risk'
  | 'origin'
  | 'freshness'
  | 'coverage'
  | 'eligibility'
  | 'provenance'
  | 'requestedResolved'
  | 'degradationProfile'
  | 'runState'
  | 'pipeline'
  | 'qa'
  | 'evidence'
  | 'p3';

export interface LocalizedText {
  'pt-PT': string;
  en: string;
}

export interface HelpTopic {
  id: HelpTopicId;
  title: LocalizedText;
  summary: LocalizedText;
  explanation: LocalizedText;
  source: LocalizedText;
  limitation: LocalizedText;
  profiles: readonly string[];
}

export interface BetaCapabilityLink {
  id: string;
  label: LocalizedText;
  description: LocalizedText;
  href: (areaCode: string | null) => string | null;
  status: 'available-in-beta' | 'v2-equivalent' | 'defer' | 'owner-decision';
}

export interface TechnicalLabel {
  code: string;
  label: LocalizedText;
  detail: LocalizedText;
}

export function localize(locale: UiV2Locale, text: LocalizedText) {
  return text[locale] ?? text.en;
}
