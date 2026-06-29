import { describe, expect, it } from 'vitest';
import { getUiV2Capabilities } from './capabilities';
import { getUiV2Pages } from './navigation/pageRegistry';

describe('UI v2 capabilities', () => {
  it('limits unsigned visitors to the public product surface', () => {
    const capabilities = getUiV2Capabilities(null);

    expect(capabilities.has('demo.read')).toBe(true);
    expect(capabilities.has('area.read')).toBe(true);
    expect(capabilities.has('data_context.read')).toBe(true);
    expect(capabilities.has('help.read')).toBe(true);
    expect(capabilities.has('risk.read')).toBe(false);
    expect(capabilities.has('pipeline.read')).toBe(false);
    expect(capabilities.has('run.read')).toBe(false);
    expect(capabilities.has('scenario.read')).toBe(false);
    expect(capabilities.has('qa.read')).toBe(false);
    expect(capabilities.has('evidence.read')).toBe(false);
    expect(capabilities.has('p3.read')).toBe(false);
    expect(capabilities.has('simulation.execute')).toBe(false);
    expect(capabilities.has('admin.read')).toBe(false);
  });

  it('maps simulation execution only to existing Admin and Sim roles', () => {
    expect(getUiV2Capabilities({ roles: ['Admin'] }).has('simulation.execute')).toBe(true);
    expect(getUiV2Capabilities({ roles: ['Sim'] }).has('simulation.execute')).toBe(true);
    expect(getUiV2Capabilities({ roles: ['Pipeline'] }).has('simulation.execute')).toBe(false);
  });

  it('restricts unknown roles to the public demo/help surface', () => {
    const capabilities = getUiV2Capabilities({ roles: ['Reviewer'] });

    expect([...capabilities].sort()).toEqual(['demo.read', 'help.read']);
  });

  it('keeps proportional administration scoped to Admin without creating new roles', () => {
    const admin = getUiV2Capabilities({ roles: ['Admin'] });
    const sim = getUiV2Capabilities({ roles: ['Sim'] });
    const pipeline = getUiV2Capabilities({ roles: ['Pipeline'] });

    expect(admin.has('admin.read')).toBe(true);
    expect(admin.has('admin.execute')).toBe(true);
    expect(sim.has('admin.read')).toBe(false);
    expect(sim.has('admin.execute')).toBe(false);
    expect(pipeline.has('admin.read')).toBe(false);
    expect(pipeline.has('pipeline.read')).toBe(true);
    expect(pipeline.has('p3.read')).toBe(false);
    expect(sim.has('pipeline.read')).toBe(false);
    expect(sim.has('scenario.read')).toBe(true);
  });

  it('derives navigation entries from capabilities', () => {
    const navigation = getUiV2Pages(new Set(['demo.read', 'data_context.read', 'help.read']));

    expect(navigation.map((item) => item.id)).toEqual(['demo', 'context']);
  });

  it('exposes mission, quality and evidence as separate read-only Pipeline tasks', () => {
    const navigation = getUiV2Pages(getUiV2Capabilities({ roles: ['Pipeline'] }));

    expect(navigation.map((item) => item.id)).toEqual([
      'demo',
      'context',
      'mission',
      'risk',
      'runs',
      'pipeline',
      'quality',
      'evidence',
    ]);
    expect(navigation.some((item) => item.id === 'deployments')).toBe(false);
  });
});
