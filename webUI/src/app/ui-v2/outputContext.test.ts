import { describe, expect, it } from 'vitest';
import { createUiV2RuntimeSummaryFixture } from './fixtures';
import { buildUiV2RiskReadModel } from './outputContext';

describe('UI v2 risk read model', () => {
  it('maps existing runtime summary fields without recalculating risk', () => {
    const model = buildUiV2RiskReadModel({ summary: createUiV2RuntimeSummaryFixture() }, 'en');

    expect(model.canShowScore).toBe(true);
    expect(model.scoreDisplay).toBe('0.78');
    expect(model.classDisplay).toBe('Very high');
    expect(
      model.contextFields.some((field) => field.key === 'provenance' && field.value === 'Candidate Parameter Set V1.0'),
    ).toBe(true);
  });

  it('does not present blocked output as score zero', () => {
    const fixture = createUiV2RuntimeSummaryFixture({
      scoreComponents: {
        ...createUiV2RuntimeSummaryFixture().scoreComponents!,
        npScore: null,
        calculationStatus: 'Blocked',
      },
      areaOperationalState: {
        ...createUiV2RuntimeSummaryFixture().areaOperationalState!,
        aggregateRiskScore: 0,
        operationalStatusReason: 'Blocked by missing input',
      },
    });

    const model = buildUiV2RiskReadModel({ summary: fixture }, 'en');

    expect(model.state).toBe('blocked');
    expect(model.canShowScore).toBe(false);
    expect(model.scoreDisplay).toBeNull();
    expect(model.classDisplay).toBeNull();
  });

  it('deduplicates repeated limitations from the existing runtime projections', () => {
    const model = buildUiV2RiskReadModel(
      {
        summary: createUiV2RuntimeSummaryFixture({
          limitations: [{ code: 'limited', message: 'Limited antecedent history' }],
          scoreComponents: {
            ...createUiV2RuntimeSummaryFixture().scoreComponents!,
            limitations: 'Limited antecedent history; Candidate defaults',
          },
          indexComparison: {
            ...createUiV2RuntimeSummaryFixture().indexComparison!,
            limitations: 'Limited antecedent history; Candidate defaults',
          },
        }),
      },
      'en',
    );

    expect(model.limitations).toEqual(['Limited antecedent history', 'Candidate defaults']);
  });

  it('uses explicit no-data state when the contract is absent', () => {
    const model = buildUiV2RiskReadModel({ summary: null }, 'pt-PT');

    expect(model.state).toBe('no-data');
    expect(model.canShowScore).toBe(false);
    expect(model.area).toBe('Desconhecido');
  });
});
