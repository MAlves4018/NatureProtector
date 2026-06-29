import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { cwd } from 'node:process';
import { resolve } from 'node:path';
import openApiContract from './contracts/backoffice-openapi-runtime.contract.json';

const webUiRoot = cwd();
const contract = openApiContract as {
  paths: Record<string, Record<string, { security?: string[]; responses?: string[] }>>;
  components: {
    schemas: Record<string, { properties: Record<string, unknown>; required?: string[] }>;
  };
};

describe('API/frontend OpenAPI contract parity', () => {
  it('keeps RuntimeSummaryResponse frontend fields aligned with the OpenAPI schema', () => {
    const frontend = readFrontendTypeSources();

    const backendFields = extractSchemaFields('RuntimeSummaryResponse');
    const frontendFields = extractInterfaceFields(frontend, 'RuntimeSummaryResponse');

    expect(frontendFields).toEqual(backendFields);
  });

  it('keeps runtime index comparison additions visible to TypeScript through OpenAPI', () => {
    const frontend = readFrontendTypeSources();

    const backendFields = extractSchemaFields('RuntimeIndexComparisonSummaryResponse');
    const frontendFields = extractInterfaceFields(frontend, 'RuntimeIndexComparisonSummaryResponse');

    expect(frontendFields).toEqual(backendFields);
  });

  it('keeps runtime API client routes aligned with secured OpenAPI paths', () => {
    const apiSource = readFileSync(resolve(webUiRoot, 'src/app/services/api.ts'), 'utf8');
    const runtimeRoutes = [
      ['/control/runtime/summary', '/api/control/runtime/summary', 'get'],
      ['/control/runtime/runs', '/api/control/runtime/runs', 'post'],
      ['/control/runtime/runs/${runId}/audit', '/api/control/runtime/runs/{runId}/audit', 'get'],
      ['/control/runtime/runs/${runId}/timings', '/api/control/runtime/runs/{runId}/timings', 'get'],
      ['/control/runtime/observability/health', '/api/control/runtime/observability/health', 'get'],
      ['/control/runtime/observability/rabbitmq', '/api/control/runtime/observability/rabbitmq', 'get'],
      ['/control/runtime/observability/evidence', '/api/control/runtime/observability/evidence', 'get'],
    ] as const;

    for (const [clientPath, openApiPath, method] of runtimeRoutes) {
      expect(apiSource).toContain(clientPath);
      const operation = contract.paths[openApiPath]?.[method];
      expect(operation, `${method.toUpperCase()} ${openApiPath}`).toBeTruthy();
      expect(operation.security).toEqual(['Bearer']);
      expect(operation.responses).toContain('401');
      expect(operation.responses).toContain('403');
    }
  });
});

function extractSchemaFields(schemaName: string) {
  return Object.keys(contract.components.schemas[schemaName].properties);
}

function extractInterfaceFields(source: string, interfaceName: string) {
  const match = source.match(new RegExp(`export interface ${interfaceName} \\{(?<body>[\\s\\S]*?)\\n\\}`));
  expect(match?.groups?.body).toBeTruthy();

  return match!
    .groups!.body.split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => line.split(':')[0].replace('?', '').trim());
}

function readFrontendTypeSources() {
  const typesRoot = resolve(webUiRoot, 'src/app/types');
  const barrel = readFileSync(resolve(typesRoot, 'index.tsx'), 'utf8');
  const modulePaths = Array.from(barrel.matchAll(/export \* from ['"](?<path>[^'"]+)['"];?/g))
    .map((match) => match.groups?.path)
    .filter((value): value is string => Boolean(value));

  return modulePaths
    .map((modulePath) => readFileSync(resolve(typesRoot, `${modulePath.replace(/^\.\//, '')}.ts`), 'utf8'))
    .join('\n');
}
