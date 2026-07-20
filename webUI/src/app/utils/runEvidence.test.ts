import { describe, expect, it } from 'vitest';
import {
  directEvidenceAssociationLabel,
  formatRunProfiles,
  resolveRunProfiles,
  resolveRunSensorCount,
} from './runEvidence';

describe('run evidence normalization', () => {
  it('prefers the resolved camelCase API contract', () => {
    const run = {
      runOverrides: {
        resolved: {
          degradationProfile: 'missing-readings',
          degradationProfiles: ['missing-readings'],
        },
      },
    };

    expect(resolveRunProfiles(run)).toEqual(['missing-readings']);
    expect(formatRunProfiles(run)).toBe('missing-readings');
  });

  it('falls back to persisted snake_case metadata', () => {
    const run = {
      metadataJson: JSON.stringify({
        run_overrides: {
          resolved: {
            degradation_profile: 'lag/delay',
            degradation_profiles: ['lag/delay'],
          },
        },
      }),
    };

    expect(resolveRunProfiles(run)).toEqual(['lag/delay']);
  });

  it('keeps the canonical nominal profile instead of presenting an ambiguous empty value', () => {
    expect(formatRunProfiles({ runOverrides: { resolved: { degradationProfiles: ['none'] } } })).toBe('none');
    expect(formatRunProfiles({})).toBe('none');
  });

  it('resolves sensor count without requiring runOverrides on SimulationRunResponse', () => {
    expect(
      resolveRunSensorCount({
        runOverrides: {
          resolved: {
            sensorCount: 2,
          },
        },
      }),
    ).toBe(2);

    expect(
      resolveRunSensorCount({
        metadataJson: JSON.stringify({
          sensor_count: 3,
        }),
      }),
    ).toBe(3);

    expect(
      resolveRunSensorCount({
        metadataJson: JSON.stringify({
          run_overrides: {
            resolved: {
              sensor_count: 4,
            },
          },
        }),
      }),
    ).toBe(4);

    expect(resolveRunSensorCount({})).toBeNull();
  });

  it('distinguishes structural association from general catalog availability', () => {
    expect(directEvidenceAssociationLabel({ evidenceId: 'evidence-1' })).toBe('Associada diretamente');
    expect(directEvidenceAssociationLabel({ evidenceId: null })).toBe('Não associada estruturalmente');
    expect(directEvidenceAssociationLabel(null)).toBe('Não associada estruturalmente');
  });
});
