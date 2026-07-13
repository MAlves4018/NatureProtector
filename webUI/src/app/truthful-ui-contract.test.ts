import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { DEGRADATION_PROFILE_OPTIONS } from './content/technicalLabels';
import { isRuntimeLaunchAvailable } from './state/SimulationContext';

function source(relativePath: string) {
  return readFileSync(new URL(relativePath, import.meta.url), 'utf8');
}

describe('R1M-001 truthful UI contract', () => {
  it('keeps the degradation profiles implemented by the current simulator', () => {
    expect(DEGRADATION_PROFILE_OPTIONS).toEqual(expect.arrayContaining(['lag/delay', 'duplicate', 'out-of-order']));
  });

  it('does not advertise runtime launch in production frontend builds', () => {
    expect(isRuntimeLaunchAvailable('production')).toBe(false);
  });

  it('does not synthesize QA executions or database results in the browser', () => {
    expect(source('./state/QaTestContext.tsx')).not.toMatch(/Math\.random|Executed via QA Test Suite UI/);
    expect(source('./pages/DatabaseQueriesPage.tsx')).not.toMatch(/simulateQuery|Query executada:/);
  });
});
