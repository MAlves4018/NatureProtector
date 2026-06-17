import { readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

describe('routing imports', () => {
  it('keeps browser routing hooks on react-router-dom', () => {
    const sourceRoot = dirname(fileURLToPath(import.meta.url));
    const offenders = findSourceFiles(sourceRoot)
      .filter(filePath => !filePath.endsWith('.test.ts') && !filePath.endsWith('.test.tsx'))
      .filter(filePath => /\bfrom\s+['"]react-router['"]/.test(readFileSync(filePath, 'utf8')))
      .map(filePath => relative(sourceRoot, filePath).replaceAll('\\', '/'))
      .sort();

    expect(offenders).toEqual([]);
  });

  it('keeps frontend environment access limited to Vite public variables', () => {
    const sourceRoot = dirname(fileURLToPath(import.meta.url));
    const offenders = findSourceFiles(sourceRoot)
      .filter(filePath => !filePath.endsWith('.test.ts') && !filePath.endsWith('.test.tsx'))
      .flatMap(filePath => {
        const relativePath = relative(sourceRoot, filePath).replaceAll('\\', '/');
        const source = readFileSync(filePath, 'utf8');
        const processEnvHits = [...source.matchAll(/\bprocess\.env\b/g)]
          .map(match => `${relativePath}:${lineNumberFor(source, match.index ?? 0)} uses process.env`);
        const privateViteEnvHits = findImportMetaEnvAccesses(source)
          .filter(access => access.variable === null || !access.variable.startsWith('VITE_'))
          .map(access => `${relativePath}:${access.line} uses ${access.expression}`);

        return [...processEnvHits, ...privateViteEnvHits];
      })
      .sort();

    expect(offenders).toEqual([]);
  });

  it('keeps browser app source free of Node-only imports', () => {
    const sourceRoot = dirname(fileURLToPath(import.meta.url));
    const offenders = findSourceFiles(sourceRoot)
      .filter(filePath => !filePath.endsWith('.test.ts') && !filePath.endsWith('.test.tsx'))
      .flatMap(filePath => {
        const relativePath = relative(sourceRoot, filePath).replaceAll('\\', '/');
        const source = readFileSync(filePath, 'utf8');
        return findStaticModuleImports(source)
          .filter(importedModule => isNodeOnlyModule(importedModule.moduleName))
          .map(importedModule => `${relativePath}:${importedModule.line} imports ${importedModule.moduleName}`);
      })
      .sort();

    expect(offenders).toEqual([]);
  });

  it('keeps browser app console logging free of user, token and session data', () => {
    const sourceRoot = dirname(fileURLToPath(import.meta.url));
    const offenders = findSourceFiles(sourceRoot)
      .filter(filePath => !filePath.endsWith('.test.ts') && !filePath.endsWith('.test.tsx'))
      .flatMap(filePath => {
        const relativePath = relative(sourceRoot, filePath).replaceAll('\\', '/');
        const source = readFileSync(filePath, 'utf8');
        return findConsoleStatements(source)
          .filter(statement => sensitiveConsoleTerms.some(term => statement.text.toLowerCase().includes(term)))
          .map(statement => `${relativePath}:${statement.line} logs sensitive session/user context`);
      })
      .sort();

    expect(offenders).toEqual([]);
  });
});

function findSourceFiles(directory: string): string[] {
  return readdirSync(directory)
    .flatMap(entry => {
      const path = join(directory, entry);
      const stats = statSync(path);

      if (stats.isDirectory()) {
        return findSourceFiles(path);
      }

      return /\.(ts|tsx)$/.test(entry) ? [path] : [];
    });
}

function lineNumberFor(source: string, index: number): number {
  return source.slice(0, index).split('\n').length;
}

function findImportMetaEnvAccesses(source: string): Array<{ expression: string; line: number; variable: string | null }> {
  return [...source.matchAll(/\bimport\.meta\.env(?:\.([A-Za-z0-9_]+)|\[['"]([^'"]+)['"]\]|\[([^\]]+)\])?/g)]
    .map(match => {
      const variable = match[1] ?? match[2] ?? null;

      return {
        expression: match[0],
        line: lineNumberFor(source, match.index ?? 0),
        variable,
      };
    });
}

function findStaticModuleImports(source: string): Array<{ line: number; moduleName: string }> {
  return [...source.matchAll(/\bfrom\s+['"]([^'"]+)['"]|\bimport\(\s*['"]([^'"]+)['"]\s*\)|\brequire\(\s*['"]([^'"]+)['"]\s*\)/g)]
    .map(match => ({
      line: lineNumberFor(source, match.index ?? 0),
      moduleName: match[1] ?? match[2] ?? match[3],
    }));
}

function isNodeOnlyModule(moduleName: string): boolean {
  const normalizedModuleName = moduleName.startsWith('node:') ? moduleName.slice('node:'.length) : moduleName;

  return nodeOnlyModules.has(normalizedModuleName);
}

function findConsoleStatements(source: string): Array<{ line: number; text: string }> {
  return [...source.matchAll(/\bconsole\.(?:log|warn|error|info|debug)\s*\(([^;\n]*)/g)]
    .map(match => ({
      line: lineNumberFor(source, match.index ?? 0),
      text: match[0],
    }));
}

const nodeOnlyModules = new Set([
  'assert',
  'buffer',
  'child_process',
  'cluster',
  'crypto',
  'dgram',
  'dns',
  'fs',
  'http',
  'https',
  'module',
  'net',
  'os',
  'path',
  'perf_hooks',
  'process',
  'readline',
  'stream',
  'tls',
  'tty',
  'url',
  'util',
  'vm',
  'worker_threads',
  'zlib',
]);

const sensitiveConsoleTerms = [
  'authorization',
  'bearer',
  'credential',
  'currentuser',
  'login',
  'logout',
  'password',
  'refreshtoken',
  'roles',
  'session',
  'storedtoken',
  'token',
  'user',
];
