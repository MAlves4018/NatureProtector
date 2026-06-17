import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const scriptPath = fileURLToPath(new URL('./check-npm-audit.mjs', import.meta.url));
const allowlistPath = fileURLToPath(new URL('./npm-audit-allowlist.json', import.meta.url));

test('accepts valid npm audit report without blockers', () => {
  const fixture = createFixture();
  const result = runAuditScript(fixture, 'clean');

  assert.equal(result.status, 0, result.stderr);
  const policy = readPolicy(fixture);
  assert.equal(policy.ok, true);
  assert.equal(policy.blocking.length, 0);
});

test('allows only the known Vite esbuild advisory chain from the explicit allowlist', () => {
  const fixture = createFixture();
  const result = runAuditScript(fixture, 'allowed-vite-chain');

  assert.equal(result.status, 0, result.stderr);
  const policy = readPolicy(fixture);
  assert.equal(policy.ok, true);
  assert.equal(policy.allowed.length, 3);
  assert.deepEqual(
    policy.allowed.map((entry) => entry.package).sort(),
    ['@vitejs/plugin-react', 'esbuild', 'vite']);
});

test('blocks an allowed advisory when a new dependency path appears', () => {
  const fixture = createFixture({
    dependencies: {},
    devDependencies: {
      '@vitejs/plugin-react': '4.7.0',
      vite: '6.4.3',
      'other-tool': '1.0.0'
    }
  });
  const result = runAuditScript(fixture, 'allowed-vite-chain');

  assert.equal(result.status, 1);
  const policy = readPolicy(fixture);
  assert.equal(policy.ok, false);
  assert.deepEqual(policy.blocking[0].uncoveredDependencyPaths, [['other-tool', 'esbuild']]);
});

test('blocks a new direct high runtime advisory', () => {
  const fixture = createFixture({
    dependencies: { 'bad-runtime': '1.0.0' },
    devDependencies: {}
  });
  const result = runAuditScript(fixture, 'direct-runtime-high');

  assert.equal(result.status, 1);
  const policy = readPolicy(fixture);
  assert.equal(policy.ok, false);
  assert.equal(policy.blocking[0].reason, 'high_runtime_blocks');
});

test('blocks a high transitive advisory reachable from runtime dependencies', () => {
  const fixture = createFixture({
    dependencies: { 'app-lib': '1.0.0' },
    devDependencies: {}
  });
  const result = runAuditScript(fixture, 'transitive-runtime-high');

  assert.equal(result.status, 1);
  const policy = readPolicy(fixture);
  assert.equal(policy.ok, false);
  assert.equal(policy.blocking[0].package, 'bad-transitive');
  assert.equal(policy.blocking[0].reason, 'high_runtime_blocks');
});

test('fails when npm audit stdout is invalid JSON', () => {
  const fixture = createFixture();
  const result = runAuditScript(fixture, 'invalid-json');

  assert.equal(result.status, 1);
  assert.equal(readDiagnostics(fixture).reason, 'npm_audit_invalid_json');
});

test('fails when npm audit stdout is empty', () => {
  const fixture = createFixture();
  const result = runAuditScript(fixture, 'empty-stdout');

  assert.equal(result.status, 1);
  assert.equal(readDiagnostics(fixture).reason, 'npm_audit_empty_stdout');
});

test('fails when npm audit returns an unexpected execution failure', () => {
  const fixture = createFixture();
  const result = runAuditScript(fixture, 'network-failure');

  assert.equal(result.status, 1);
  assert.equal(readDiagnostics(fixture).reason, 'npm_audit_unexpected_exit_code');
});

test('fails when the audit JSON schema is missing expected fields', () => {
  const fixture = createFixture();
  const result = runAuditScript(fixture, 'missing-field');

  assert.equal(result.status, 1);
  assert.equal(readDiagnostics(fixture).reason, 'npm_audit_policy_evaluation_failed');
});

test('fails when a dependency path cannot be classified', () => {
  const fixture = createFixture();
  const result = runAuditScript(fixture, 'unknown-path-high');

  assert.equal(result.status, 1);
  const policy = readPolicy(fixture);
  assert.equal(policy.blocking[0].reason, 'dependency_path_unclassified');
});

function createFixture({
  dependencies = {},
  devDependencies = {
    '@vitejs/plugin-react': '4.7.0',
    vite: '6.4.3'
  }
} = {}) {
  const directory = mkdtempSync(join(tmpdir(), 'np-npm-audit-'));
  writeFileSync(
    join(directory, 'package.json'),
    `${JSON.stringify({
      name: 'webui-audit-fixture',
      version: '0.0.0',
      private: true,
      dependencies,
      devDependencies
    }, null, 2)}\n`);
  writeFileSync(
    join(directory, 'package-lock.json'),
    `${JSON.stringify(createPackageLock({ dependencies, devDependencies }), null, 2)}\n`);
  return {
    directory,
    outputPath: join(directory, 'npm-audit.json')
  };
}

function createPackageLock({ dependencies, devDependencies }) {
  return {
    name: 'webui-audit-fixture',
    version: '0.0.0',
    lockfileVersion: 3,
    requires: true,
    packages: {
      '': {
        name: 'webui-audit-fixture',
        version: '0.0.0',
        dependencies,
        devDependencies
      },
      'node_modules/@vitejs/plugin-react': {
        version: '4.7.0',
        dev: true,
        peerDependencies: { vite: '^6.0.0' }
      },
      'node_modules/vite': {
        version: '6.4.3',
        dev: true,
        dependencies: { esbuild: '^0.25.0' }
      },
      'node_modules/esbuild': {
        version: '0.25.0',
        dev: true
      },
      'node_modules/bad-runtime': {
        version: '1.0.0'
      },
      'node_modules/app-lib': {
        version: '1.0.0',
        dependencies: { 'bad-transitive': '1.0.0' }
      },
      'node_modules/other-tool': {
        version: '1.0.0',
        dev: true,
        dependencies: { esbuild: '^0.25.0' }
      },
      'node_modules/bad-transitive': {
        version: '1.0.0'
      }
    }
  };
}

function runAuditScript(fixture, scenario) {
  const fakeAudit = `
const scenario = process.argv[1];
const reports = ${JSON.stringify(auditReports())};
if (scenario === 'invalid-json') {
  process.stdout.write('{ invalid');
  process.exit(1);
}
if (scenario === 'empty-stdout') {
  process.exit(1);
}
if (scenario === 'network-failure') {
  process.stderr.write('registry unavailable');
  process.exit(2);
}
const report = reports[scenario];
if (!report) {
  process.stderr.write('unknown scenario');
  process.exit(2);
}
process.stdout.write(JSON.stringify(report.body));
process.exit(report.exitCode);
`;

  return spawnSync(
    process.execPath,
    [scriptPath, fixture.outputPath],
    {
      cwd: fixture.directory,
      encoding: 'utf8',
      env: {
        ...process.env,
        NP_NPM_AUDIT_COMMAND: process.execPath,
        NP_NPM_AUDIT_ARGS: JSON.stringify(['-e', fakeAudit, scenario]),
        NP_NPM_AUDIT_ALLOWLIST: allowlistPath,
        NP_NPM_AUDIT_NOW: '2026-06-15T00:00:00Z'
      }
    });
}

function readPolicy(fixture) {
  return JSON.parse(readFileSync(companionPath(fixture.outputPath, 'policy'), 'utf8'));
}

function readDiagnostics(fixture) {
  return JSON.parse(readFileSync(companionPath(fixture.outputPath, 'diagnostics'), 'utf8'));
}

function companionPath(outputPath, suffix) {
  return resolve(dirname(outputPath), `npm-audit.${suffix}.json`);
}

function auditReports() {
  return {
    clean: {
      exitCode: 0,
      body: baseReport({})
    },
    'allowed-vite-chain': {
      exitCode: 1,
      body: baseReport({
        '@vitejs/plugin-react': {
          name: '@vitejs/plugin-react',
          severity: 'high',
          isDirect: true,
          via: ['vite'],
          effects: [],
          range: '4.0.0-beta.0 - 5.1.4',
          nodes: ['node_modules/@vitejs/plugin-react']
        },
        vite: {
          name: 'vite',
          severity: 'high',
          isDirect: true,
          via: ['esbuild'],
          effects: ['@vitejs/plugin-react'],
          range: '4.2.0-beta.0 - 8.0.3',
          nodes: ['node_modules/vite']
        },
        esbuild: {
          name: 'esbuild',
          severity: 'high',
          isDirect: false,
          via: [advisory('esbuild')],
          effects: ['vite'],
          range: '0.17.0 - 0.28.0',
          nodes: ['node_modules/esbuild']
        }
      })
    },
    'direct-runtime-high': {
      exitCode: 1,
      body: baseReport({
        'bad-runtime': {
          name: 'bad-runtime',
          severity: 'high',
          isDirect: true,
          via: [advisory('bad-runtime')],
          effects: [],
          range: '<=1.0.0',
          nodes: ['node_modules/bad-runtime']
        }
      })
    },
    'transitive-runtime-high': {
      exitCode: 1,
      body: baseReport({
        'bad-transitive': {
          name: 'bad-transitive',
          severity: 'high',
          isDirect: false,
          via: [advisory('bad-transitive')],
          effects: [],
          range: '<=1.0.0',
          nodes: ['node_modules/bad-transitive']
        }
      })
    },
    'missing-field': {
      exitCode: 1,
      body: {
        auditReportVersion: 2
      }
    },
    'unknown-path-high': {
      exitCode: 1,
      body: baseReport({
        'unknown-package': {
          name: 'unknown-package',
          severity: 'high',
          isDirect: false,
          via: [advisory('unknown-package')],
          effects: [],
          range: '<=1.0.0',
          nodes: ['node_modules/unknown-package']
        }
      })
    }
  };
}

function baseReport(vulnerabilities) {
  const high = Object.values(vulnerabilities).filter((entry) => entry.severity === 'high').length;
  const critical = Object.values(vulnerabilities).filter((entry) => entry.severity === 'critical').length;
  return {
    auditReportVersion: 2,
    vulnerabilities,
    metadata: {
      vulnerabilities: {
        info: 0,
        low: 0,
        moderate: 0,
        high,
        critical,
        total: Object.keys(vulnerabilities).length
      }
    }
  };
}

function advisory(packageName) {
  return {
    source: 1120679,
    name: packageName,
    dependency: packageName,
    title: `${packageName} advisory`,
    url: 'https://github.com/advisories/GHSA-gv7w-rqvm-qjhr',
    severity: 'high',
    range: '<=1.0.0'
  };
}
