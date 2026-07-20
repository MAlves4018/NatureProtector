import { expect, test, type Locator, type Page, type TestInfo } from '@playwright/test';
import crypto from 'node:crypto';
import fs from 'node:fs/promises';
import path from 'node:path';

interface CaptureMetadata {
  simulationRunId: string;
  simulationRunIds?: string[];
  scenario: string;
  claim: string;
  limitation: string;
}

interface CaptureRecord {
  captureId: string;
  file: string;
  route: string;
  capturedAtUtc: string;
  baselineId: string;
  baselineSha256: string;
  simulationRunId: string;
  simulationRunIds: string[];
  scenario: string;
  claim: string;
  limitation: string;
  resolution: string;
  width: number;
  height: number;
  sha256: string;
}

const enabled = process.env.NP_POINT5_REPORT_EVIDENCE === '1';
const screenshotRoot = process.env.NP_POINT5_SCREENSHOT_ROOT;
const baselineId = process.env.NP_POINT5_BASELINE_ID ?? 'UNKNOWN';
const baselineSha256 = process.env.NP_POINT5_BASELINE_SHA256 ?? 'UNKNOWN';
const heroRunId = process.env.NP_POINT5_HERO_RUN_ID ?? '';
const nominalRunId = process.env.NP_POINT5_NOMINAL_RUN_ID ?? '';
const heroScenario = process.env.NP_POINT5_HERO_SCENARIO ?? 'scenario_c';
const nominalScenario = process.env.NP_POINT5_NOMINAL_SCENARIO ?? 'scenario_b';

test.describe('point 5 report evidence', () => {
  test.skip(!enabled, 'Requires NP_POINT5_REPORT_EVIDENCE=1.');

  test('captures correlated, report-readable evidence for the canonical hero run', async ({ page }, testInfo) => {
    test.setTimeout(240_000);
    test.skip(testInfo.project.name !== 'desktop', 'One deterministic desktop capture is sufficient.');
    if (!screenshotRoot) throw new Error('NP_POINT5_SCREENSHOT_ROOT is required.');
    if (!heroRunId || !nominalRunId) throw new Error('Hero and nominal SimulationRunId values are required.');

    await login(page);
    await writeCaptureEnvironment(page, testInfo);

    await page.goto('/simulation');
    await expect(page.getByRole('heading', { name: /^Simulação$/i })).toBeVisible({ timeout: 20_000 });
    const area = page.getByLabel(/selecionar área/i);
    if ((await area.inputValue()) !== 'proenca-a-nova') await area.selectOption('proenca-a-nova');
    await page.getByLabel(/selecionar cenário/i).selectOption(heroScenario);
    await page.getByRole('button', { name: /^3 Duração$/i }).click();
    await page.getByLabel(/^Ciclos$/i).fill('2');
    await page.getByLabel(/intervalo/i).fill('1');
    await page.getByRole('button', { name: /^4 Degradações$/i }).click();
    const missing = page.getByRole('checkbox', { name: 'missing-readings' });
    if (!(await missing.isChecked())) await missing.check();
    await page.getByRole('button', { name: /^6 Revisão$/i }).click();
    await expect(page.locator('.ui-review-summary').first()).toContainText(heroScenario);
    await captureLocator(page, page.locator('.ui-review-panel').first(), 'hero-configuration', {
      simulationRunId: heroRunId,
      scenario: heroScenario,
      claim: 'Configuração controlada reproduzida para o cenário C, seed 42, dois ciclos, dois sensores e missing-readings.',
      limitation: 'A configuração foi reconstituída a partir da run persistida; não é uma fotografia histórica anterior ao lançamento.',
    });

    await page.goto(`/runs?runId=${encodeURIComponent(heroRunId)}`);
    await expect(page.getByRole('heading', { name: 'Espaço da execução' })).toBeVisible({ timeout: 30_000 });
    await expect(page.getByLabel(/selecionar execução/i)).toHaveValue(heroRunId);
    await expect(page.locator('.ui-run-identity')).toContainText(heroRunId);
    await expect(page.getByText('Cockpit da execução')).toBeVisible({ timeout: 20_000 });

    const common: Omit<CaptureMetadata, 'claim' | 'limitation'> = {
      simulationRunId: heroRunId,
      scenario: heroScenario,
    };

    await captureLocator(page, page.locator('.ui-run-identity'), 'hero-identity', {
      ...common,
      claim: 'Identifica inequivocamente a SimulationRunId, o cenário e a área da hero run degradada.',
      limitation: 'Execução local controlada; não representa produção.',
    });
    await captureLocator(page, page.getByTestId('run-summary-panel'), 'hero-summary', {
      ...common,
      claim: 'Mostra o estado terminal e os campos persistidos da execução degradada.',
      limitation: 'Os estados técnicos canónicos permanecem em inglês quando correspondem a enums do contrato.',
    });
    await captureLocator(page, page.locator('.ui-science-summary'), 'hero-scientific-metrics', {
      ...common,
      claim: 'Apresenta NP Score, FWI, KBDI e proxy português para a mesma SimulationRunId.',
      limitation: 'FWI, KBDI e parâmetros territoriais incluem defaults candidatos explicitados pela auditoria.',
    });

    const runTabs = page.getByLabel('Detalhes da run');
    await runTabs.getByRole('button', { name: /Contabilidade|Accounting/i }).click();
    await captureLocator(page, page.getByTestId('run-accounting-panel'), 'hero-accounting', {
      ...common,
      claim: 'Reconcilia esperados, aceites, ausentes, processados, pendentes e settlement da hero run.',
      limitation: 'A reconciliação usa os contadores persistidos expostos pela API run-scoped.',
    });

    await runTabs.getByRole('button', { name: /^Qualidade$/i }).click();
    await captureLocator(page, page.getByTestId('run-quality-panel'), 'hero-quality', {
      ...common,
      claim: 'Mostra perdas, rejeições, quarentena, retries e avaliações de risco associadas à run.',
      limitation: 'Os payloads detalhados do classificador de qualidade ainda não são persistidos integralmente.',
    });

    await runTabs.getByRole('button', { name: /Evidência|Evidence/i }).click();
    await captureLocator(page, page.getByTestId('run-evidence-panel'), 'hero-evidence', {
      ...common,
      claim: 'Mostra o pacote exportável, a associação direta a evidenceId e o DataScope run-scoped da hero run.',
      limitation: 'A disponibilidade do catálogo continua a ser demonstrada separadamente na superfície de evidência.',
    });

    await page.goto(`/queries?runId=${encodeURIComponent(heroRunId)}`);
    await expect(page.getByRole('heading', { name: 'Consultas preparadas' })).toBeVisible({ timeout: 20_000 });
    const selectedQualityPreset = await selectQualityPreset(page);
    await page.getByRole('button', { name: /Executar preset/i }).click();
    await expect(page.locator('.ui-table tbody tr').first()).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText(`Resultado associado a SimulationRunId: ${heroRunId}`)).toBeVisible();
    const queryResult = page
      .getByText(`Resultado associado a SimulationRunId: ${heroRunId}`)
      .locator('xpath=ancestor::section[contains(@class,"ui-card")][1]');
    await captureLocator(page, queryResult, 'hero-query-quality', {
      ...common,
      claim: 'Consulta preparada filtrada pela mesma SimulationRunId para suportar integridade, confiança e cobertura.',
      limitation: selectedQualityPreset
        ? 'Resultado local run-scoped; não constitui uma análise estatística multi-seed.'
        : 'Não foi encontrado um seletor nominal específico; foi executado o preset disponível e a limitação fica registada.',
    });

    await page.goto(
      `/scenario-compare?runA=${encodeURIComponent(nominalRunId)}&runB=${encodeURIComponent(heroRunId)}`,
    );
    await expect(page.getByRole('heading', { name: 'Comparar execuções' })).toBeVisible({ timeout: 20_000 });
    await expect
      .poll(() => page.getByLabel('Run A').locator('option').count(), { timeout: 20_000 })
      .toBeGreaterThanOrEqual(2);
    await page.getByLabel('Run A').selectOption(nominalRunId);
    await page.getByLabel('Run B').selectOption(heroRunId);
    await page.getByRole('button', { name: /^Comparar$/i }).click();
    const profileRow = page.locator('tbody tr', { hasText: 'Perfis de degradação resolvidos' });
    await expect(profileRow).toContainText('none');
    await expect(profileRow).toContainText('missing-readings');
    const associationRow = page.locator('tbody tr', { hasText: 'Ligação direta a evidenceId' });
    await expect(associationRow).toContainText('Associada diretamente');
    const comparisonCard = profileRow.locator('xpath=ancestor::section[contains(@class,"ui-card")][1]');
    await captureLocator(page, comparisonCard, 'hero-vs-nominal-comparison', {
      simulationRunId: heroRunId,
      simulationRunIds: [nominalRunId, heroRunId],
      scenario: `${nominalScenario} vs ${heroScenario}`,
      claim: 'Compara explicitamente a run nominal e a hero run degradada, incluindo perfis resolvidos, accounting e ligação direta a evidenceId.',
      limitation: 'Comparação de uma execução por cenário com seed controlada; não suporta claims de comportamento médio.',
    });

    await page.goto(`/evidence?runId=${encodeURIComponent(heroRunId)}`);
    await expect(page.getByRole('heading', { name: 'Cockpit de evidência' })).toBeVisible({ timeout: 20_000 });
    await captureViewport(page, 'hero-evidence-catalog', {
      ...common,
      claim: 'Demonstra que o catálogo local de evidência está disponível e navegável.',
      limitation: 'A navegação do catálogo é evidência de disponibilidade local; a ligação estrutural por operation.evidenceId é validada na comparação run-scoped.',
    });

    await page.goto(`/pipeline?runId=${encodeURIComponent(heroRunId)}`);
    await expect(page.getByRole('heading', { name: /Pipeline e observabilidade/i })).toBeVisible({ timeout: 20_000 });
    await captureViewport(page, 'hero-observability', {
      ...common,
      claim: 'Apresenta a superfície local de pipeline e observabilidade usada na validação.',
      limitation: 'A captura da aplicação não substitui um dashboard Grafana filtrado pela SimulationRunId.',
    });
  });
});

async function login(page: Page) {
  const username = process.env.NP_UI_USERNAME;
  const password = process.env.NP_UI_PASSWORD;
  if (!username || !password) throw new Error('NP_UI_USERNAME and NP_UI_PASSWORD are required.');

  await page.goto('/login');
  await page.getByLabel('Username or email').fill(username);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: /^Sign in$/ }).click();
  await expect(page.getByRole('heading', { name: 'Visão geral operacional' })).toBeVisible({ timeout: 30_000 });
}

async function selectQualityPreset(page: Page): Promise<boolean> {
  const matcher = /integridade|integrity|confidence|confiança|coverage|cobertura/i;
  for (const select of await page.locator('select').all()) {
    const options = await select.locator('option').allTextContents();
    const index = options.findIndex((value) => matcher.test(value));
    if (index >= 0) {
      const value = await select.locator('option').nth(index).getAttribute('value');
      if (value) await select.selectOption(value);
      return true;
    }
  }

  const candidate = page.getByRole('button', { name: matcher }).first();
  if (await candidate.isVisible().catch(() => false)) {
    await candidate.click();
    return true;
  }
  return false;
}

async function capturePage(page: Page, name: string, metadata: CaptureMetadata) {
  await capture(page, null, name, metadata, true);
}

async function captureViewport(page: Page, name: string, metadata: CaptureMetadata) {
  await capture(page, null, name, metadata, false);
}

async function captureLocator(page: Page, locator: Locator, name: string, metadata: CaptureMetadata) {
  await expect(locator).toBeVisible();
  await capture(page, locator, name, metadata, false);
}

async function capture(
  page: Page,
  locator: Locator | null,
  name: string,
  metadata: CaptureMetadata,
  fullPage: boolean,
) {
  if (!screenshotRoot) return;
  await fs.mkdir(screenshotRoot, { recursive: true });
  const filePath = path.join(screenshotRoot, `${name}.png`);
  if (locator) {
    await locator.screenshot({ path: filePath });
  } else {
    await page.screenshot({ path: filePath, fullPage });
  }

  const bytes = await fs.readFile(filePath);
  const width = bytes.readUInt32BE(16);
  const height = bytes.readUInt32BE(20);
  const record: CaptureRecord = {
    captureId: name,
    file: path.basename(filePath),
    route: routeOf(page.url()),
    capturedAtUtc: new Date().toISOString(),
    baselineId,
    baselineSha256,
    simulationRunId: metadata.simulationRunId,
    simulationRunIds: metadata.simulationRunIds ?? [metadata.simulationRunId],
    scenario: metadata.scenario,
    claim: metadata.claim,
    limitation: metadata.limitation,
    resolution: `${width}x${height}`,
    width,
    height,
    sha256: crypto.createHash('sha256').update(bytes).digest('hex'),
  };
  await upsertRegister(record);
}

async function upsertRegister(record: CaptureRecord) {
  if (!screenshotRoot) return;
  const jsonPath = path.join(screenshotRoot, 'capture-register.json');
  let records: CaptureRecord[] = [];
  try {
    records = JSON.parse(await fs.readFile(jsonPath, 'utf8')) as CaptureRecord[];
  } catch {
    records = [];
  }
  const next = [...records.filter((item) => item.captureId !== record.captureId), record].sort((a, b) =>
    a.captureId.localeCompare(b.captureId),
  );
  await fs.writeFile(jsonPath, `${JSON.stringify(next, null, 2)}\n`, 'utf8');

  const headers = [
    'captureId',
    'file',
    'route',
    'capturedAtUtc',
    'baselineId',
    'baselineSha256',
    'simulationRunId',
    'simulationRunIds',
    'scenario',
    'claim',
    'limitation',
    'resolution',
    'width',
    'height',
    'sha256',
  ];
  const rows = [headers.join(','), ...next.map((item) => headers.map((key) => csv(field(item, key))).join(','))];
  await fs.writeFile(path.join(screenshotRoot, 'capture-register.csv'), `${rows.join('\n')}\n`, 'utf8');
}

async function writeCaptureEnvironment(page: Page, testInfo: TestInfo) {
  if (!screenshotRoot) return;
  await fs.mkdir(screenshotRoot, { recursive: true });
  const viewport = page.viewportSize();
  await fs.writeFile(
    path.join(screenshotRoot, 'capture-environment.json'),
    `${JSON.stringify(
      {
        capturedAtUtc: new Date().toISOString(),
        baselineId,
        baselineSha256,
        browserProject: testInfo.project.name,
        viewport,
        baseUrl: testInfo.project.use.baseURL ?? null,
        heroRunId,
        nominalRunId,
      },
      null,
      2,
    )}\n`,
    'utf8',
  );
}

function routeOf(value: string) {
  const url = new URL(value);
  return `${url.pathname}${url.search}`;
}

function field(record: CaptureRecord, key: string): string | number {
  const value = record[key as keyof CaptureRecord];
  return Array.isArray(value) ? value.join(';') : value;
}

function csv(value: string | number) {
  const text = String(value ?? '');
  return /[",\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
}
