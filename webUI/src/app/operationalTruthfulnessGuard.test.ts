import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

function source(relativePath: string) {
  return readFileSync(resolve(process.cwd(), 'src/app', relativePath), 'utf8');
}

describe('operational truthfulness guard', () => {
  it.each([
    'state/QaTestContext.tsx',
    'pages/DatabaseQueriesPage.tsx',
    'components/SimulationProgress.tsx',
  ])('%s contains no random or timer-driven execution result', (relativePath) => {
    const text = source(relativePath);
    expect(text).not.toMatch(/Math\.random\s*\(/);
    expect(text).not.toMatch(/\bsetTimeout\s*\(/);
    expect(text).not.toMatch(/\bsetInterval\s*\(/);
  });

  it('does not reintroduce known misleading global claims', () => {
    expect(source('pages/DeploymentHealthPage.tsx')).not.toContain('All systems operational');
    expect(source('pages/QualityEvidencePage.tsx')).not.toContain('Latest test execution');
    expect(source('pages/DatabaseQueriesPage.tsx')).not.toContain('Query executada:');
  });
});
