import type { UiLocale, UiMessageKey } from '../i18n';

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
  | 'evidence';

export interface HelpTopic {
  id: HelpTopicId;
  title: Record<'pt-PT' | 'en', string>;
  summary: Record<'pt-PT' | 'en', string>;
  explanation: Record<'pt-PT' | 'en', string>;
  source: Record<'pt-PT' | 'en', string>;
  limitation: Record<'pt-PT' | 'en', string>;
  profiles: string[];
}

export interface BetaCapabilityLink {
  id: string;
  label: Record<'pt-PT' | 'en', string>;
  description: Record<'pt-PT' | 'en', string>;
  href: (areaCode: string | null) => string | null;
  status: string;
}

export interface TechnicalLabel {
  code: string;
  label: Record<'pt-PT' | 'en', string>;
  detail: Record<'pt-PT' | 'en', string>;
}

export type Copy = (key: UiMessageKey) => string;

export function localize(locale: UiLocale, obj: Record<'pt-PT' | 'en', string>): string {
  return obj[locale];
}
