import { expect, test } from '@playwright/test';

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
    await page.getByLabel(/selecionar área/i).selectOption('proenca-a-nova');
    const profile = process.env.LIVE_PROFILE ?? 'nominal';
    await page.getByLabel(/selecionar cenário/i).selectOption(profile === 'missing' ? 'scenario_c' : 'scenario_b');
    await page.getByRole('button', { name: /^3 Duração$/i }).click();
    await page.getByLabel(/^Ciclos$/i).fill('2');
    await page.getByLabel(/intervalo/i).fill('1');
    if (profile === 'missing') {
      await page.getByRole('button', { name: /^4 Degradações$/i }).click();
      await page.getByRole('checkbox', { name: 'missing-readings' }).check();
    }
    await page.getByRole('button', { name: /^6 Revisão$/i }).click();
    await page.getByRole('button', { name: /Iniciar simulação/i }).click();
    await expect(page.getByText('OperationId')).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText('SystemCompleted').first()).toBeVisible({ timeout: 120_000 });

    await primaryNav.getByRole('button', { name: /^Simulações$/ }).click();
    await primaryNav.getByRole('button', { name: /^Execuções$/ }).click();
    await expect(page.getByRole('heading', { name: 'Run workspace' })).toBeVisible();

    await primaryNav.getByRole('button', { name: /^Análise e evidência$/ }).click();
    await primaryNav.getByRole('button', { name: /^Pipeline$/ }).click();
    await expect(page.getByRole('heading', { name: /Pipeline e observabilidade/i })).toBeVisible();

    await primaryNav.getByRole('button', { name: /^Simulações$/ }).click();
    await primaryNav.getByRole('button', { name: /^Consultas preparadas$/ }).click();
    await expect(page.getByRole('heading', { name: 'Consultas preparadas' })).toBeVisible();
    await page.getByRole('button', { name: /Executar consulta preparada/i }).click();
    await expect(page.locator('.ui-table tbody tr').first()).toBeVisible();

    await primaryNav.getByRole('button', { name: /^Comparar cenários B vs C$/ }).click();
    await page.getByLabel(/selecionar área/i).selectOption('proenca-a-nova');
    await page.getByRole('button', { name: /^Comparar$/i }).click();
    await expect(page.locator('.ui-table tbody tr').first()).toBeVisible();

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
});
