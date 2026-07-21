import { expect, test } from '@playwright/test';
import path from 'node:path';

const screenshotRoot = process.env.UI_REVISION_SCREENSHOTS;

test.describe('live local runtime', () => {
  test.skip(process.env.LIVE_RUNTIME !== '1', 'Requires the official local runtime launcher.');

  test('login, operational surfaces and simulation launcher use real services', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop', 'One live execution is sufficient.');
    const username = process.env.NP_UI_USERNAME;
    const password = process.env.NP_UI_PASSWORD;
    if (!username || !password) throw new Error('NP_UI_USERNAME and NP_UI_PASSWORD are required.');

    const unexpectedResponses: string[] = [];
    page.on('response', (response) => {
      if (response.status() >= 500) unexpectedResponses.push(`${response.status()} ${response.url()}`);
    });

    await page.goto('/login');
    await page.getByLabel('Username or email').fill(username);
    await page.getByLabel('Password').fill(password);
    await page.getByRole('button', { name: /^Sign in$/ }).click();
    await expect(page.getByRole('heading', { name: 'Visão geral operacional' })).toBeVisible({ timeout: 20_000 });
    const primaryNav = page.getByLabel('Navegação principal');

    const area = page.getByLabel(/selecionar área/i);
    await area.selectOption('proenca-a-nova');
    await expect(page.getByText(/Saúde global/)).toBeVisible();

    await page.goto('/simulation');
    await expect(page.getByRole('heading', { name: /^Simulação$/i })).toBeVisible();
    const simulationArea = page.getByLabel(/selecionar área/i);
    if ((await simulationArea.inputValue()) !== 'proenca-a-nova') {
      await simulationArea.selectOption('proenca-a-nova');
    }
    const profile = process.env.LIVE_PROFILE ?? 'nominal';
    const scenarioCode = process.env.LIVE_SCENARIO ?? (profile === 'missing' ? 'scenario_c' : 'scenario_b');
    await page.getByLabel(/selecionar cenário/i).selectOption(scenarioCode);
    await page.getByRole('button', { name: /^3 Duração$/i }).click();
    await page.getByLabel(/^Ciclos$/i).fill('2');
    await page.getByLabel(/intervalo/i).fill('1');
    if (profile === 'missing') {
      await page.getByRole('button', { name: /^4 Degradações$/i }).click();
      await page.getByRole('checkbox', { name: 'missing-readings' }).check();
    }
    await page.getByRole('button', { name: /^6 Revisão$/i }).click();
    await expect(page.locator('.ui-review-summary').first().getByText(scenarioCode, { exact: true })).toBeVisible();
    const launchResponsePromise = page.waitForResponse(
      (response) =>
        response.url().includes('/api/control/runtime/runs') &&
        response.request().method() === 'POST',
    );

    await page.getByRole('button', { name: /Iniciar simulação/i }).click();

    const launchResponse = await launchResponsePromise;
    if (launchResponse.status() === 429) {
      const retryAfter = launchResponse.headers()['retry-after'] ?? 'unknown';
      throw new Error(
        `Simulation launch was rate-limited. Retry-After=${retryAfter}s; ` +
          `evidenceRunId=${process.env.NP_EVIDENCE_RUN_ID ?? 'missing'}.`,
      );
    }

    expect(
      launchResponse.ok(),
      `Simulation launch failed with HTTP ${launchResponse.status()}: ${await launchResponse.text()}`,
    ).toBe(true);

    await expect(page.getByText('OperationId')).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText('SystemCompleted').first()).toBeVisible({ timeout: 120_000 });
    const launchedRunIdValue = page
      .locator('.ui-review-panel .ui-definition-list')
      .locator('dt')
      .filter({ hasText: /^SimulationRunId$/ })
      .locator('xpath=following-sibling::dd[1]');
    await expect(launchedRunIdValue).toHaveText(
      /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i,
      { timeout: 30_000 },
    );
    const launchedRunId = (await launchedRunIdValue.textContent())?.trim();
    expect(launchedRunId).toBeTruthy();

    await primaryNav.getByRole('button', { name: /^Simulações$/ }).click();
    await primaryNav.getByRole('button', { name: /^Execuções$/ }).click();
    await expect(page.getByRole('heading', { name: 'Espaço da execução' })).toBeVisible();
    await expect(page.getByLabel(/selecionar execução/i)).toHaveValue(launchedRunId!, { timeout: 20_000 });
    const selectedRunId = launchedRunId!;
    await expect(page).toHaveURL(new RegExp(`runId=${selectedRunId}`));
    await page.reload();
    await expect(page.getByLabel(/selecionar execução/i)).toHaveValue(selectedRunId);
    await expect(page.getByText('Cockpit da execução')).toBeVisible({ timeout: 20_000 });
    const scientificSummary = page.locator('.ui-science-summary');
    await expect(scientificSummary.getByText('NP Score', { exact: true })).toBeVisible();
    await expect(scientificSummary.getByText('FWI', { exact: true })).toBeVisible();
    await expect(scientificSummary.getByText('KBDI', { exact: true })).toBeVisible();
    await expect(scientificSummary.getByText('Portuguese Proxy', { exact: true })).toBeVisible();
    const readAccounting = () =>
      page.evaluate(async (runId) => {
        const headers = { Authorization: `Bearer ${localStorage.getItem('token') ?? ''}` };
        const [auditResponse, operationResponse] = await Promise.all([
          fetch(`/api/control/runtime/runs/${runId}/audit`, { headers }),
          fetch(`/api/control/runtime/runs/${runId}/operation`, { headers }),
        ]);
        if (!auditResponse.ok || !operationResponse.ok) {
          throw new Error(
            `Run-scoped accounting failed: audit=${auditResponse.status} operation=${operationResponse.status}`,
          );
        }
        const audit = (await auditResponse.json()) as {
          expectedEvents: number;
          acceptedReadings: number;
          missingEvents: number;
          riskAssessments: number;
        };
        const operation = (await operationResponse.json()) as {
          simulationRunId: string;
          accounting: { processedInbox: number; settled: boolean };
        };
        return { ...audit, ...operation.accounting, operationRunId: operation.simulationRunId };
      }, selectedRunId);
    await expect.poll(async () => (await readAccounting()).settled, { timeout: 30_000 }).toBe(true);
    const accounting = await readAccounting();
    expect(accounting.operationRunId).toBe(selectedRunId);
    expect(accounting.settled).toBe(true);
    expect(accounting.processedInbox).toBe(accounting.acceptedReadings);
    expect(accounting.riskAssessments).toBe(accounting.acceptedReadings);
    if (profile === 'missing') {
      expect(accounting.acceptedReadings).toBeLessThan(accounting.expectedEvents);
      expect(accounting.missingEvents).toBe(accounting.expectedEvents - accounting.acceptedReadings);
    } else {
      expect(accounting.acceptedReadings).toBe(accounting.expectedEvents);
      expect(accounting.missingEvents).toBe(0);
    }
    await capture(page, `live-${profile}-run`);

    await primaryNav.getByRole('button', { name: /^Análise e evidência$/ }).click();
    await primaryNav.getByRole('button', { name: /^Pipeline$/ }).click();
    await expect(page.getByRole('heading', { name: /Pipeline e observabilidade/i })).toBeVisible();

    await primaryNav.getByRole('button', { name: /^Simulações$/ }).click();
    await primaryNav.getByRole('button', { name: /^Consultas preparadas$/ }).click();
    await expect(page.getByRole('heading', { name: 'Consultas preparadas' })).toBeVisible();
    await page.getByRole('button', { name: /Executar preset/i }).click();
    await expect(page.locator('.ui-table tbody tr').first()).toBeVisible();
    await expect(page.getByText(`Resultado associado a SimulationRunId: ${selectedRunId}`)).toBeVisible();
    await capture(page, `live-${profile}-query`);

    if (process.env.LIVE_SKIP_COMPARISON !== '1') {
      await primaryNav.getByRole('button', { name: /^Comparar cenários B vs C$/ }).click();
      await expect
        .poll(() => page.getByLabel('Run A').locator('option').count(), { timeout: 20_000 })
        .toBeGreaterThanOrEqual(2);
      const runOptions = await page
        .getByLabel('Run A')
        .locator('option')
        .evaluateAll((options) => options.map((option) => (option as HTMLOptionElement).value).filter(Boolean));

      if (runOptions.length >= 2) {
        await page.getByLabel('Run A').selectOption(runOptions[0]);
        await page.getByLabel('Run B').selectOption(runOptions[1]);
        await page.getByRole('button', { name: /^Comparar$/i }).click();
        await expect(page.locator('.ui-table tbody tr').first()).toBeVisible();
        await capture(page, `live-${profile}-comparison`);
      } else {
        testInfo.annotations.push({
          type: 'comparison',
          description: 'Skipped because only one completed run is currently available.',
        });
      }
    }

    await primaryNav.getByRole('button', { name: /^Análise e evidência$/ }).click();
    await primaryNav.getByRole('button', { name: /^Evidence Explorer$/ }).click();
    await expect(page.getByRole('heading', { name: 'Cockpit de evidência' })).toBeVisible();

    await primaryNav.getByRole('button', { name: /^Operações e release$/ }).click();
    await primaryNav.getByRole('button', { name: /^Deployments$/ }).click();
    await expect(page.getByRole('heading', { name: 'Deployments' })).toBeVisible();
    await expect(page.getByText(/Queued significa/i)).toBeVisible();

    await primaryNav.getByRole('button', { name: /^Admin$/ }).click();
    await primaryNav.getByRole('button', { name: /^Administração$/ }).click();
    await expect(page.getByRole('heading', { name: /Administração proporcional/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Runtime reset' })).toBeVisible();
    await page.getByLabel(/Escreva RESET_RUNTIME_STATE/i).fill('RESET_RUNTIME_STATE');
    await page.getByRole('button', { name: 'Executar dry-run' }).click();
    await expect(page.locator('.ui-reset-result, .ui-notice.ui-error')).toBeVisible({ timeout: 30_000 });

    expect(unexpectedResponses).toEqual([]);
  });

  test('runtime reset is rejected while active', async ({ page }, testInfo) => {
    test.setTimeout(180_000);
    test.skip(process.env.LIVE_RESET_PROOF !== '1', 'Requires explicit local destructive-reset authorization.');
    test.skip(testInfo.project.name !== 'desktop', 'One live reset proof is sufficient.');
    const username = process.env.NP_UI_USERNAME;
    const password = process.env.NP_UI_PASSWORD;
    if (!username || !password) throw new Error('NP_UI_USERNAME and NP_UI_PASSWORD are required.');

    await page.goto('/login');
    await page.getByLabel('Username or email').fill(username);
    await page.getByLabel('Password').fill(password);
    await page.getByRole('button', { name: /^Sign in$/ }).click();
    await expect(page.getByRole('heading', { name: 'Visão geral operacional' })).toBeVisible({ timeout: 20_000 });
    await page.getByLabel(/selecionar área/i).selectOption('proenca-a-nova');
    await page.goto('/simulation');
    await page.getByLabel(/selecionar cenário/i).selectOption('scenario_b');
    await page.getByRole('button', { name: /^3 Duração$/i }).click();
    await page.getByLabel(/^Ciclos$/i).fill('8');
    await page.getByLabel(/intervalo/i).fill('1');
    await page.getByRole('button', { name: /^6 Revisão$/i }).click();
    await page.getByRole('button', { name: /Iniciar simulação/i }).click();
    const operationLabel = page.locator('.ui-review-panel .ui-definition-list').getByText('OperationId', { exact: true });
    await expect(operationLabel).toBeVisible({ timeout: 30_000 });
    const operationId = (await operationLabel.locator('xpath=following-sibling::dd[1]').textContent())?.trim();
    expect(operationId).toBeTruthy();

    await page.goto('/admin');
    await page.getByLabel(/Dry-run/i).uncheck();
    await page.getByLabel(/Escreva RESET_RUNTIME_STATE/i).fill('RESET_RUNTIME_STATE');
    await page.getByRole('button', { name: 'Executar reset' }).click();
    await expect(page.getByText(/Reset requires quiescence|Reset is blocked while/i)).toBeVisible({ timeout: 30_000 });
    await capture(page, 'live-reset-blocked-active');

    await expect
      .poll(
        () =>
          page.evaluate(async (id) => {
            const response = await fetch(`/api/control/runtime/operations/${id}`, {
              headers: { Authorization: `Bearer ${localStorage.getItem('token') ?? ''}` },
            });
            if (!response.ok) return false;
            const operation = (await response.json()) as {
              state?: string;
              accounting?: { settled?: boolean };
            };
            return operation.state === 'SystemCompleted' && operation.accounting?.settled === true;
          }, operationId),
        { timeout: 120_000 },
      )
      .toBe(true);
  });

  test('runtime reset is accepted after settlement', async ({ page }, testInfo) => {
    test.setTimeout(120_000);
    test.skip(process.env.LIVE_RESET_PROOF !== '1', 'Requires explicit local destructive-reset authorization.');
    test.skip(testInfo.project.name !== 'desktop', 'One live reset proof is sufficient.');
    const username = process.env.NP_UI_USERNAME;
    const password = process.env.NP_UI_PASSWORD;
    if (!username || !password) throw new Error('NP_UI_USERNAME and NP_UI_PASSWORD are required.');

    await page.goto('/login');
    await page.getByLabel('Username or email').fill(username);
    await page.getByLabel('Password').fill(password);
    await page.getByRole('button', { name: /^Sign in$/ }).click();
    await expect(page.getByRole('heading', { name: 'Visão geral operacional' })).toBeVisible({ timeout: 20_000 });
    await expect
      .poll(
        () =>
          page.evaluate(async () => {
            const response = await fetch('/api/control/runtime/reset', {
              method: 'POST',
              headers: {
                Authorization: `Bearer ${localStorage.getItem('token') ?? ''}`,
                'Content-Type': 'application/json',
              },
              body: JSON.stringify({ scope: 'runtime-only', confirm: 'RESET_RUNTIME_STATE', dryRun: true }),
            });
            const result = (await response.json()) as { status?: string };
            return result.status ?? `HTTP ${response.status}`;
          }),
        { timeout: 30_000 },
      )
      .toBe('DryRun');
    await page.goto('/admin');
    await page.getByLabel(/Dry-run/i).uncheck();
    await page.getByLabel(/Escreva RESET_RUNTIME_STATE/i).fill('RESET_RUNTIME_STATE');
    await page.getByRole('button', { name: 'Executar reset' }).click();
    await expect(page.getByText('Completed', { exact: true })).toBeVisible({ timeout: 60_000 });
    await capture(page, 'live-reset-completed-settled');
  });
});

async function capture(page: import('@playwright/test').Page, name: string) {
  if (!screenshotRoot) return;
  await page.screenshot({ path: path.join(screenshotRoot, `${name}.png`), fullPage: true });
}
