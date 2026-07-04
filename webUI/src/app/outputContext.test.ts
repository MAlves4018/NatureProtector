import { describe, expect, it } from 'vitest';
import { createUiRuntimeSummaryFixture } from './fixtures';
import { buildUiRiskReadModel } from './outputContext';

describe('risk read model', () => {
  it('maps existing runtime summary fields without recalculating risk', () => {
    const model = buildUiRiskReadModel({ summary: createUiRuntimeSummaryFixture() }, 'en');

    expect(model.canShowScore).toBe(true);
    expect(model.scoreDisplay).toBe('0.78');
    expect(model.classDisplay).toBe('Very high');
    expect(
      model.contextFields.some((field) => field.key === 'provenance' && field.value === 'Candidate Parameter Set V1.0'),
    ).toBe(true);
  });

  it('does not present blocked output as score zero', () => {
    const fixture = createUiRuntimeSummaryFixture({
      scoreComponents: {
        ...createUiRuntimeSummaryFixture().scoreComponents!,
        npScore: null,
        calculationStatus: 'Blocked',
      },
      areaOperationalState: {
        ...createUiRuntimeSummaryFixture().areaOperationalState!,
        aggregateRiskScore: 0,
        operationalStatusReason: 'Blocked by missing input',
      },
    });

    const model = buildUiRiskReadModel({ summary: fixture }, 'en');

    expect(model.state).toBe('blocked');
    expect(model.canShowScore).toBe(false);
    expect(model.scoreDisplay).toBeNull();
    expect(model.classDisplay).toBeNull();
  });

  it('deduplicates repeated limitations from the existing runtime projections', () => {
    const model = buildUiRiskReadModel(
      {
        summary: createUiRuntimeSummaryFixture({
          limitations: [{ code: 'limited', message: 'Limited antecedent history' }],
          scoreComponents: {
            ...createUiRuntimeSummaryFixture().scoreComponents!,
            limitations: 'Limited antecedent history; Candidate defaults',
          },
          indexComparison: {
            ...createUiRuntimeSummaryFixture().indexComparison!,
            limitations: 'Limited antecedent history; Candidate defaults',
          },
        }),
      },
      'en',
    );

    expect(model.limitations).toEqual(['Limited antecedent history', 'Candidate defaults']);
  });

  it('uses explicit no-data state when the contract is absent', () => {
    const model = buildUiRiskReadModel({ summary: null }, 'pt-PT');

    expect(model.state).toBe('no-data');
    expect(model.canShowScore).toBe(false);
    expect(model.area).toBe('Desconhecido');
  });
});


