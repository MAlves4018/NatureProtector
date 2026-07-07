import { describe, expect, it } from 'vitest';
import { getUiCapabilities } from './capabilities';
import { getUiPages } from './navigation/pageRegistry';

describe('capabilities', () => {
  it('limits unsigned visitors to the public product surface', () => {
    const capabilities = getUiCapabilities(null);

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
    expect(getUiCapabilities({ roles: ['Admin'] }).has('simulation.execute')).toBe(true);
    expect(getUiCapabilities({ roles: ['Sim'] }).has('simulation.execute')).toBe(true);
    expect(getUiCapabilities({ roles: ['Pipeline'] }).has('simulation.execute')).toBe(false);
  });

  it('restricts unknown roles to the public demo/help surface', () => {
    const capabilities = getUiCapabilities({ roles: ['Reviewer'] });

    expect([...capabilities].sort()).toEqual(['demo.read', 'help.read']);
  });

  it('keeps proportional administration scoped to Admin without creating new roles', () => {
    const admin = getUiCapabilities({ roles: ['Admin'] });
    const sim = getUiCapabilities({ roles: ['Sim'] });
    const pipeline = getUiCapabilities({ roles: ['Pipeline'] });

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
    const navigation = getUiPages(new Set(['demo.read', 'data_context.read', 'help.read']));

    expect(navigation.map((item) => item.id)).toEqual(['demo', 'about', 'context']);
  });

  it('exposes mission, quality and evidence as separate read-only Pipeline tasks', () => {
    const navigation = getUiPages(getUiCapabilities({ roles: ['Pipeline'] }));

    expect(navigation.map((item) => item.id)).toEqual([
      'demo',
      'dashboard',
      'about',
      'context',
      'mission',
      'risk',
      'runs',
      'scenario-compare',
      'pipeline',
      'qa',
      'evidence',
    ]);
    expect(navigation.some((item) => item.id === 'deployments')).toBe(false);
  });
});


