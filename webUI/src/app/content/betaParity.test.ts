import { describe, expect, it } from 'vitest';
import { BETA_CAPABILITIES } from './betaParity';

describe('BETA_CAPABILITIES', () => {
  it('keeps legacy beta capabilities explicit and unmounted instead of inventing routes', () => {
    expect(BETA_CAPABILITIES.map((capability) => capability.id)).toEqual([
      'monitoring',
      'map',
      'runtime-monitor',
      'scenario-lab',
      'evidence-comparison',
      'flow-model',
    ]);

    for (const capability of BETA_CAPABILITIES) {
      expect(capability.status).toBe('legacy-not-yet-ported');
      expect(capability.href(null)).toBeNull();
      expect(capability.label['pt-PT']).not.toHaveLength(0);
      expect(capability.description.en).toContain('Legacy');
    }
  });
});
