import { readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const webRoot = resolve(scriptDirectory, '..');
const repositoryRoot = resolve(webRoot, '..');

const expected = {
  nodeFileVersion: '20.17.0',
  node22CiVersion: '22.16.0',
  nodeEngine: '>=20.17.0 <21 || >=22.16.0 <23',
  npmEngine: '>=10.8.2',
  packageManager: 'npm@10.8.2',
  react: '18.3.1',
  reactDom: '18.3.1'
};

const failures = [];

function fail(message) {
  failures.push(message);
}

function readText(path) {
  return readFileSync(path, 'utf8').trim();
}

function readJson(path) {
  return JSON.parse(readFileSync(path, 'utf8'));
}

function expectEqual(label, actual, expectedValue) {
  if (actual !== expectedValue) {
    fail(`${label} expected '${expectedValue}' but found '${actual ?? '<missing>'}'.`);
  }
}

function expectMajorRange(packageName, actual, major) {
  if (typeof actual !== 'string' || (!actual.startsWith(`${major}.`) && !actual.startsWith(`^${major}.`))) {
    fail(`${packageName} must stay on ${major}.x, found '${actual ?? '<missing>'}'.`);
  }
}

const packageJson = readJson(join(webRoot, 'package.json'));
const packageLock = readJson(join(webRoot, 'package-lock.json'));
const dependencies = packageJson.dependencies ?? {};
const devDependencies = packageJson.devDependencies ?? {};
const peerDependencies = packageJson.peerDependencies ?? {};

expectEqual('package.json packageManager', packageJson.packageManager, expected.packageManager);
expectEqual('package.json engines.node', packageJson.engines?.node, expected.nodeEngine);
expectEqual('package.json engines.npm', packageJson.engines?.npm, expected.npmEngine);
expectEqual('webUI/.nvmrc', readText(join(webRoot, '.nvmrc')), expected.nodeFileVersion);
expectEqual('webUI/.node-version', readText(join(webRoot, '.node-version')), expected.nodeFileVersion);
expectEqual('dependencies.react', dependencies.react, expected.react);
expectEqual('dependencies.react-dom', dependencies['react-dom'], expected.reactDom);

for (const packageName of Object.keys(dependencies).filter(name => name.startsWith('@types/'))) {
  fail(`${packageName} must be a devDependency, not a runtime dependency.`);
}

if ('react' in peerDependencies || 'react-dom' in peerDependencies) {
  fail('This application must keep react/react-dom in dependencies, not peerDependencies.');
}

expectMajorRange('@types/react', devDependencies['@types/react'], 18);
expectMajorRange('@types/react-dom', devDependencies['@types/react-dom'], 18);
expectMajorRange('@types/node', devDependencies['@types/node'], 20);

const rootLockPackage = packageLock.packages?.[''];
if (!rootLockPackage) {
  fail('package-lock.json is missing the root package entry.');
} else {
  expectEqual('package-lock root engines.node', rootLockPackage.engines?.node, expected.nodeEngine);
  expectEqual('package-lock root dependencies.react', rootLockPackage.dependencies?.react, expected.react);
  expectEqual('package-lock root dependencies.react-dom', rootLockPackage.dependencies?.['react-dom'], expected.reactDom);
  expectMajorRange('package-lock root devDependencies.@types/react', rootLockPackage.devDependencies?.['@types/react'], 18);
  expectMajorRange('package-lock root devDependencies.@types/react-dom', rootLockPackage.devDependencies?.['@types/react-dom'], 18);
  expectMajorRange('package-lock root devDependencies.@types/node', rootLockPackage.devDependencies?.['@types/node'], 20);
}

const allowedWorkflowNodeVersions = new Set([expected.nodeFileVersion, expected.node22CiVersion]);
for (const workflow of ['engineering-foundations.yml', 'release-candidate.yml', 'security.yml']) {
  const workflowPath = join(repositoryRoot, '.github', 'workflows', workflow);
  const content = readText(workflowPath);
  const matches = [...content.matchAll(/^\s*node-version:\s*["']?([^"'\s#]+)["']?/gm)];
  if (matches.length === 0) {
    fail(`${workflow} does not declare a setup-node node-version.`);
  }

  for (const match of matches) {
    const version = match[1];
    if (!allowedWorkflowNodeVersions.has(version)) {
      fail(`${workflow} uses unsupported node-version '${version}'.`);
    }
  }
}

if (failures.length > 0) {
  console.error('Toolchain drift detected:');
  for (const failure of failures) {
    console.error(`- ${failure}`);
  }

  process.exit(1);
}

console.log('Toolchain manifests are aligned for React 18, Node 20.17.0/22.16.0 and npm 10.8.2.');
