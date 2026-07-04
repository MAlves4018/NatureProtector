import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const severityRank = { info: 0, low: 1, moderate: 2, high: 3, critical: 4 };

export function classifyAuditPolicy({ auditReport, packageJson, packageLock, allowlist, now = new Date() }) {
  validateAuditReport(auditReport);
  validateAllowlist(allowlist);

  const dependencyPaths = buildDependencyPaths(packageJson, packageLock);
  const vulnerabilities = Object.values(auditReport.vulnerabilities);
  const results = [];
  const blocking = [];
  const allowed = [];
  const visible = [];

  for (const vulnerability of vulnerabilities) {
    validateVulnerability(vulnerability);
    const paths = dependencyPaths.get(vulnerability.name) ?? [];
    if (paths.length === 0) {
      blocking.push({
        package: vulnerability.name,
        severity: vulnerability.severity,
        reason: 'dependency_path_unclassified',
      });
      results.push({
        package: vulnerability.name,
        severity: vulnerability.severity,
        decision: 'blocked',
        reason: 'dependency_path_unclassified',
      });
      continue;
    }

    const advisoryIds = collectAdvisoryIds(vulnerability.name, auditReport.vulnerabilities);
    const classification = classifyPaths(paths);
    const rank = severityRank[vulnerability.severity] ?? 0;
    const matchingAllowlist = findMatchingAllowlistForAllPaths({
      vulnerability,
      paths,
      advisoryIds,
      classification,
      allowlist,
      now,
    });

    const result = {
      package: vulnerability.name,
      severity: vulnerability.severity,
      advisoryIds,
      dependencyPaths: paths.map((path) => path.packages),
      classification,
      range: vulnerability.range,
    };

    if (rank >= severityRank.critical) {
      blocking.push({ ...result, reason: 'critical_blocks' });
      results.push({ ...result, decision: 'blocked', reason: 'critical_blocks' });
      continue;
    }

    if (rank >= severityRank.high && classification.runtimeReachable) {
      blocking.push({ ...result, reason: 'high_runtime_blocks' });
      results.push({ ...result, decision: 'blocked', reason: 'high_runtime_blocks' });
      continue;
    }

    if (rank >= severityRank.high) {
      if (matchingAllowlist.covered) {
        allowed.push({
          ...result,
          decision: 'allowed',
          allowlistReasons: [...new Set(matchingAllowlist.entries.map((entry) => entry.reason))],
          expiresOn: [...new Set(matchingAllowlist.entries.map((entry) => entry.expiresOn))].sort().at(0),
        });
        results.push({
          ...result,
          decision: 'allowed',
          reason: 'all_dependency_paths_allowlisted',
          expiresOn: [...new Set(matchingAllowlist.entries.map((entry) => entry.expiresOn))].sort().at(0),
        });
      } else {
        blocking.push({
          ...result,
          reason: 'high_without_matching_allowlist',
          uncoveredDependencyPaths: matchingAllowlist.uncoveredPaths,
        });
        results.push({
          ...result,
          decision: 'blocked',
          reason: 'high_without_matching_allowlist',
          uncoveredDependencyPaths: matchingAllowlist.uncoveredPaths,
        });
      }
      continue;
    }

    visible.push({ ...result, decision: 'visible' });
    results.push({ ...result, decision: 'visible' });
  }

  return {
    ok: blocking.length === 0,
    totals: auditReport.metadata.vulnerabilities,
    results,
    allowed,
    visible,
    blocking,
  };
}

export function buildDependencyPaths(packageJson, packageLock) {
  if (!packageLock || typeof packageLock !== 'object' || !packageLock.packages) {
    throw new Error('package-lock.json is missing the expected packages object.');
  }

  const rootRuntime = new Set(Object.keys(packageJson.dependencies ?? {}));
  const rootDev = new Set(Object.keys(packageJson.devDependencies ?? {}));
  const result = new Map();
  const rootNames = [...new Set([...rootRuntime, ...rootDev])].sort();

  for (const rootName of rootNames) {
    const rootType = rootRuntime.has(rootName) ? 'runtime' : 'dev';
    walkDependency(rootName, [rootName], rootType, new Set([rootName]));
  }

  return result;

  function walkDependency(name, path, rootType, seen) {
    const packageEntry = packageLock.packages[`node_modules/${name}`];
    if (!packageEntry) {
      return;
    }

    if (!result.has(name)) {
      result.set(name, []);
    }

    result.get(name).push({ rootType, packages: [...path] });

    for (const childName of Object.keys(packageEntry.dependencies ?? {})) {
      if (seen.has(childName)) {
        continue;
      }

      walkDependency(childName, [...path, childName], rootType, new Set([...seen, childName]));
    }
  }
}

function classifyPaths(paths) {
  const rootTypes = [...new Set(paths.map((path) => path.rootType))].sort();
  return {
    rootTypes,
    runtimeReachable: rootTypes.includes('runtime'),
    devOnly: rootTypes.length > 0 && rootTypes.every((type) => type === 'dev'),
    value: rootTypes.includes('runtime') ? 'runtime' : 'dev',
  };
}

function validateAuditReport(report) {
  if (!report || typeof report !== 'object') {
    throw new Error('npm audit JSON root must be an object.');
  }

  if (report.auditReportVersion !== 2) {
    throw new Error('npm audit report version 2 is required.');
  }

  if (!report.vulnerabilities || typeof report.vulnerabilities !== 'object' || Array.isArray(report.vulnerabilities)) {
    throw new Error('npm audit JSON is missing vulnerabilities object.');
  }

  if (!report.metadata?.vulnerabilities || typeof report.metadata.vulnerabilities.total !== 'number') {
    throw new Error('npm audit JSON is missing metadata.vulnerabilities totals.');
  }
}

function validateVulnerability(vulnerability) {
  if (!vulnerability || typeof vulnerability !== 'object') {
    throw new Error('npm audit vulnerability entry must be an object.');
  }

  for (const field of ['name', 'severity', 'via', 'range', 'nodes']) {
    if (!(field in vulnerability)) {
      throw new Error(`npm audit vulnerability entry for ${vulnerability.name ?? '<unknown>'} is missing ${field}.`);
    }
  }
}

function validateAllowlist(allowlist) {
  if (!allowlist || typeof allowlist !== 'object' || allowlist.version !== 1) {
    throw new Error('npm audit allowlist version 1 is required.');
  }

  if (!Array.isArray(allowlist.allowedAdvisories)) {
    throw new Error('npm audit allowlist must contain allowedAdvisories array.');
  }

  for (const entry of allowlist.allowedAdvisories) {
    for (const field of [
      'advisoryId',
      'package',
      'dependencyPath',
      'classification',
      'severity',
      'reason',
      'acceptedVersionRange',
      'expiresOn',
      'revalidationTriggers',
    ]) {
      if (!(field in entry)) {
        throw new Error(`npm audit allowlist entry for ${entry.package ?? '<unknown>'} is missing ${field}.`);
      }
    }
  }
}

function collectAdvisoryIds(packageName, vulnerabilities, seen = new Set()) {
  if (seen.has(packageName)) {
    return [];
  }

  seen.add(packageName);
  const vulnerability = vulnerabilities[packageName];
  if (!vulnerability) {
    return [];
  }

  const ids = [];
  for (const via of vulnerability.via ?? []) {
    if (typeof via === 'string') {
      ids.push(...collectAdvisoryIds(via, vulnerabilities, seen));
      continue;
    }

    if (via && typeof via === 'object') {
      if (via.url) {
        ids.push(String(via.url).split('/').at(-1));
      } else if (via.source) {
        ids.push(String(via.source));
      }
    }
  }

  return [...new Set(ids)].sort();
}

function findMatchingAllowlistForAllPaths({ vulnerability, paths, advisoryIds, classification, allowlist, now }) {
  const entries = [];
  const uncoveredPaths = [];

  for (const path of paths) {
    const match = allowlist.allowedAdvisories.find((entry) => {
      if (new Date(`${entry.expiresOn}T23:59:59Z`) < now) {
        return false;
      }

      return (
        entry.package === vulnerability.name &&
        entry.severity === vulnerability.severity &&
        entry.acceptedVersionRange === vulnerability.range &&
        entry.classification === classification.value &&
        advisoryIds.includes(entry.advisoryId) &&
        arraysEqual(path.packages, entry.dependencyPath)
      );
    });

    if (match) {
      entries.push(match);
    } else {
      uncoveredPaths.push(path.packages);
    }
  }

  return {
    covered: uncoveredPaths.length === 0 && entries.length > 0,
    entries,
    uncoveredPaths,
  };
}

function arraysEqual(left, right) {
  return (
    Array.isArray(left) &&
    Array.isArray(right) &&
    left.length === right.length &&
    left.every((value, index) => value === right[index])
  );
}

function readJson(path, label) {
  try {
    return JSON.parse(readFileSync(path, 'utf8'));
  } catch (error) {
    throw new Error(`Failed to read ${label} at ${path}: ${error.message}`);
  }
}

function companionPath(outputPath, suffix) {
  return outputPath.replace(/\.json$/i, `.${suffix}.json`);
}

function writeJson(path, value) {
  writeFileSync(path, `${JSON.stringify(value, null, 2)}\n`);
}

function writeFailureArtifacts(paths, diagnostic, exitCode = null) {
  writeJson(paths.diagnostics, diagnostic);
  writeJson(paths.policy, {
    ok: false,
    blocking: [{ reason: diagnostic.reason }],
  });
  writeFileSync(paths.exitCode, exitCode === null ? 'not-started\n' : `${exitCode}\n`);
}

export function runCli(argv = process.argv, env = process.env) {
  const outputPath = resolve(argv[2] ?? 'npm-audit.json');
  const paths = {
    raw: outputPath,
    diagnostics: companionPath(outputPath, 'diagnostics'),
    policy: companionPath(outputPath, 'policy'),
    exitCode: outputPath.replace(/\.json$/i, '.exit-code.txt'),
  };
  mkdirSync(dirname(outputPath), { recursive: true });

  const command = env.NP_NPM_AUDIT_COMMAND ?? 'npm';
  const args = env.NP_NPM_AUDIT_ARGS ? JSON.parse(env.NP_NPM_AUDIT_ARGS) : ['audit', '--json'];
  const audit = spawnSync(command, args, {
    cwd: process.cwd(),
    encoding: 'utf8',
    shell: process.platform === 'win32' && !env.NP_NPM_AUDIT_COMMAND,
  });

  if (audit.error) {
    writeFileSync(paths.raw, audit.stdout ?? '');
    writeFailureArtifacts(paths, {
      reason: 'npm_audit_not_executed',
      error: audit.error.message,
      stderr: audit.stderr ?? '',
    });
    console.error(`npm audit did not execute. Diagnostics written to ${paths.diagnostics}.`);
    return 1;
  }

  const exitCode = audit.status ?? audit.signal ?? 'unknown';
  writeFileSync(paths.raw, audit.stdout ?? '');
  writeFileSync(paths.exitCode, `${exitCode}\n`);

  if (audit.status !== 0 && audit.status !== 1) {
    writeFailureArtifacts(
      paths,
      {
        reason: 'npm_audit_unexpected_exit_code',
        exitCode,
        stderr: audit.stderr ?? '',
      },
      audit.status,
    );
    console.error(`npm audit returned unexpected exit code ${exitCode}.`);
    return 1;
  }

  if (!audit.stdout || audit.stdout.trim().length === 0) {
    writeFailureArtifacts(
      paths,
      {
        reason: 'npm_audit_empty_stdout',
        exitCode,
        stderr: audit.stderr ?? '',
      },
      audit.status,
    );
    console.error('npm audit returned empty stdout.');
    return 1;
  }

  let report;
  try {
    report = JSON.parse(audit.stdout);
  } catch (error) {
    writeFailureArtifacts(
      paths,
      {
        reason: 'npm_audit_invalid_json',
        exitCode,
        parseError: error.message,
        stderr: audit.stderr ?? '',
      },
      audit.status,
    );
    console.error(`npm audit output is not valid JSON. Raw output was written to ${outputPath}.`);
    return 1;
  }

  try {
    const packageJson = readJson(resolve('package.json'), 'package.json');
    const packageLock = readJson(resolve('package-lock.json'), 'package-lock.json');
    const allowlistPath = resolve(env.NP_NPM_AUDIT_ALLOWLIST ?? 'scripts/npm-audit-allowlist.json');
    if (!existsSync(allowlistPath)) {
      throw new Error(`npm audit allowlist was not found at ${allowlistPath}.`);
    }

    const allowlist = readJson(allowlistPath, 'npm audit allowlist');
    const policy = classifyAuditPolicy({
      auditReport: report,
      packageJson,
      packageLock,
      allowlist,
      now: env.NP_NPM_AUDIT_NOW ? new Date(env.NP_NPM_AUDIT_NOW) : new Date(),
    });

    writeJson(paths.diagnostics, {
      reason: 'npm_audit_completed',
      exitCode,
      stderr: audit.stderr ?? '',
      allowlistPath,
    });
    writeJson(paths.policy, policy);

    console.log(`npm audit raw JSON written to ${outputPath}`);
    console.log(`npm audit exit code written to ${paths.exitCode}`);
    console.log(`npm audit diagnostics written to ${paths.diagnostics}`);
    console.log(`npm audit policy result written to ${paths.policy}`);
    console.log(`npm audit totals: ${JSON.stringify(policy.totals)}`);

    if (policy.allowed.length > 0) {
      console.warn('Explicitly allowed advisories:');
      for (const advisory of policy.allowed) {
        console.warn(
          `- ${advisory.severity.toUpperCase()} ${advisory.package} ` +
            `${advisory.advisoryIds.join(',')} path=${advisory.dependencyPaths.map((path) => path.join('>')).join('|')} ` +
            `expires=${advisory.expiresOn}`,
        );
      }
    }

    if (!policy.ok) {
      console.error('Blocking npm advisories or audit failures found:');
      for (const advisory of policy.blocking) {
        console.error(
          `- ${advisory.severity?.toUpperCase?.() ?? 'UNKNOWN'} ${advisory.package ?? ''} ${advisory.reason}`,
        );
      }
      return 1;
    }

    return 0;
  } catch (error) {
    writeFailureArtifacts(
      paths,
      {
        reason: 'npm_audit_policy_evaluation_failed',
        exitCode,
        error: error.message,
        stderr: audit.stderr ?? '',
      },
      audit.status,
    );
    console.error(`npm audit policy evaluation failed: ${error.message}`);
    return 1;
  }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  process.exit(runCli());
}
