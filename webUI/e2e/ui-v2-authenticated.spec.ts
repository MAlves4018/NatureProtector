import { expect, test, type Page } from '@playwright/test';
import { installUiV2ApiFixture, type ObservedApiRequest, type RoleProfile, type SummaryFailure, type SummaryState } from './ui-v2-api-fixture';

test.describe('UI v2 authenticated role flows', () => {
  test('anonymous users see landing, login, and no protected operations', async ({ page }) => {
    await installUiV2ApiFixture(page);

    await page.goto('/ui-v2');

    await expect(page.getByRole('heading', { name: 'NatureProtector UI v2' })).toBeVisible();
    await expect(page.getByRole('link', { name: /entrar/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Pipeline$/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^Simulacao$/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^Administracao$/i })).toHaveCount(0);

    await page.goto('/ui-v2?area=proenca-a-nova');
    await expect(page.getByRole('button', { name: /^Pipeline$/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^P3 experimental$/i })).toHaveCount(0);
  });

  test('Admin login can navigate health, evidence, and proportional administration', async ({ page }) => {
    const api = await installUiV2ApiFixture(page, { profile: 'Admin' });

    await signIn(page, 'Admin');

    await expect(page.getByText('Perfil ativo: Admin')).toBeVisible();
    await expect(page.getByRole('button', { name: /^Pipeline$/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Simulacao$/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Administracao$/i })).toBeVisible();

    await page.getByRole('button', { name: /^Pipeline$/i }).click();
    await expect(page.getByRole('heading', { name: /Pipeline e observabilidade/i })).toBeVisible();
    await page.getByText('Campos de runtime, temporalidade e provenance').click();
    await expect(page.getByText('Prevention.Host health')).toBeVisible();
    await expect(page.getByText('RabbitMQ health')).toBeVisible();
    await expect(page.getByText('Ingestion ready')).toBeVisible();

    await page.getByRole('button', { name: /^Qualidade e evidencia$/i }).click();
    await expect(page.getByRole('heading', { name: /Qualidade e evidencia/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Runtime smoke evidence' })).toBeVisible();
    const download = page.waitForEvent('download');
    await page.getByRole('button', { name: /Download evidence/i }).click();
    expect((await download).suggestedFilename()).toBe('ui-v2-runtime-smoke.txt');

    await page.getByRole('button', { name: /^Administracao$/i }).click();
    await expect(page.getByRole('heading', { name: /Administracao proporcional/i })).toBeVisible();
    await expect(page.getByText('Runtime reset')).toBeVisible();
    await expect(page.getByText('User/role administration')).toBeVisible();

    expect(api.requests.some(request => request.path === '/control/runtime/observability/health')).toBe(true);
    expect(api.requests.some(request => request.path === '/control/runtime/observability/evidence')).toBe(true);
    expect(allProtectedRequestsHaveBearer(api.requests)).toBe(true);
  });

  test('Sim login can validate and start a degraded runtime run without pipeline access', async ({ page }) => {
    const api = await installUiV2ApiFixture(page, { profile: 'Sim' });

    await signIn(page, 'Sim');

    await expect(page.getByRole('button', { name: /^Simulacao$/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Pipeline$/i })).toHaveCount(0);
    await page.getByRole('button', { name: /^Simulacao$/i }).click();

    await page.getByLabel(/Selecionar cenario/i).selectOption('scenario_c');
    await page.getByRole('combobox', { name: /^Degradacao$/i }).selectOption('noise');
    await expect(page.getByRole('button', { name: /Iniciar simulacao/i })).toBeEnabled();
    await page.getByRole('button', { name: /Iniciar simulacao/i }).click();

    await expect(page.getByRole('heading', { name: /Contexto de run/i })).toBeVisible();
    const startRequest = api.requests.find(request => request.method === 'POST' && request.path === '/control/runtime/runs');
    expect(startRequest?.authorization).toBe('Bearer sim-token');
    expect(startRequest?.postData).toMatchObject({
      areaCode: 'proenca-a-nova',
      scenarioCode: 'scenario_c',
      degradationProfile: 'noise',
      degradationProfiles: ['noise'],
    });
  });

  test('Pipeline login sees pipeline health, queues and audit without simulation access', async ({ page }) => {
    const api = await installUiV2ApiFixture(page, { profile: 'Pipeline' });

    await signIn(page, 'Pipeline');

    await expect(page.getByRole('button', { name: /^Pipeline$/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Qualidade e evidencia$/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Simulacao$/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^Administracao$/i })).toHaveCount(0);

    await page.getByRole('button', { name: /^Pipeline$/i }).click();
    await page.getByText('Campos de runtime, temporalidade e provenance').click();
    await expect(page.getByText('Queue state')).toBeVisible();
    await expect(page.getByText('Ingestion unacknowledged')).toBeVisible();

    await page.getByRole('button', { name: /^Execucoes$/i }).click();
    await expect(page.getByRole('heading', { name: /Auditoria de run/i })).toBeVisible();
    await expect(page.getByText('4 accepted / 1 rejected / 0 quarantined')).toBeVisible();

    expect(api.requests.some(request => request.path === '/control/runtime/observability/rabbitmq')).toBe(true);
    expect(api.requests.some(request => request.path === '/control/runtime/runs')).toBe(false);
  });
});

test.describe('UI v2 authentication and API failure states', () => {
  test('login 401 is visible and does not persist a token', async ({ page }) => {
    await installUiV2ApiFixture(page, { loginStatus: 401 });

    await page.goto('/login');
    await page.getByLabel(/Username or email/i).fill('bad-user');
    await page.getByLabel(/Password/i).fill('wrong-password');
    await page.getByRole('button', { name: /^Sign in$/i }).click();

    await expect(page.getByText('Invalid credentials')).toBeVisible();
    await expect.poll(() => page.evaluate(() => localStorage.getItem('token'))).toBeNull();
  });

  test('session expiry clears the stored token and returns to public navigation', async ({ page }) => {
    await installUiV2ApiFixture(page, { profile: 'Admin', meStatus: 401 });
    await page.addInitScript(() => localStorage.setItem('token', 'expired-token'));

    await page.goto('/ui-v2');

    await expect(page.getByRole('link', { name: /entrar/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Pipeline$/i })).toHaveCount(0);
    await expect.poll(() => page.evaluate(() => localStorage.getItem('token'))).toBeNull();
  });

  test('unknown roles stay on public-only capability surface after login', async ({ page }) => {
    await installUiV2ApiFixture(page, { profile: 'Unknown' });

    await signIn(page, 'Unknown');

    await expect(page.getByText('Perfil ativo: Observer')).toBeVisible();
    await expect(page.getByRole('button', { name: /^Visao geral$/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Pipeline$/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^Simulacao$/i })).toHaveCount(0);
  });

  for (const state of ['partial', 'stale', 'blocked', 'null', 'unknown'] as const) {
    test(`runtime summary state ${state} is surfaced without inventing green status`, async ({ page }) => {
      await installUiV2ApiFixture(page, { profile: 'Admin', summaryState: state });

      await signIn(page, 'Admin');

      await assertSummaryState(page, state);
    });
  }

  for (const failure of ['500', 'network', 'timeout'] as const) {
    test(`runtime summary ${failure} failure is surfaced as a runtime error`, async ({ page }) => {
      await installUiV2ApiFixture(page, { profile: 'Admin', summaryFailure: failure });

      await signIn(page, 'Admin');
      await openRiskPage(page);

      await expect(runtimeFailureMessage(page, failure)).toBeVisible();
    });
  }

  test('write 403 from simulation endpoint is shown to an otherwise authorized Sim profile', async ({ page }) => {
    await installUiV2ApiFixture(page, { profile: 'Sim', startRunStatus: 403 });

    await signIn(page, 'Sim');
    await page.getByRole('button', { name: /^Simulacao$/i }).click();
    await page.getByLabel(/Selecionar cenario/i).selectOption('scenario_b');
    await page.getByRole('button', { name: /Iniciar simulacao/i }).click();

    await expect(page.getByText('Forbidden by mock RBAC')).toBeVisible();
  });
});

async function signIn(page: Page, profile: RoleProfile) {
  await page.goto('/ui-v2');
  await page.getByRole('link', { name: /entrar/i }).click();
  await page.getByLabel(/Username or email/i).fill(`${profile.toLowerCase()}@natureprotector.test`);
  await page.getByLabel(/Password/i).fill('password');
  const loginResponse = page.waitForResponse(response =>
    response.url().includes('/api/users-roles/login') && response.request().method() === 'POST');
  await page.getByRole('button', { name: /^Sign in$/i }).click();
  await expect((await loginResponse).status()).toBe(200);
  await expect(page).toHaveURL(/\/ui-v2/);
  await expect.poll(() => page.evaluate(() => localStorage.getItem('token'))).toBe(`${profile.toLowerCase()}-token`);
  await page.goto('/ui-v2?area=proenca-a-nova');
  await expect(page.getByRole('heading', { name: 'NatureProtector UI v2' })).toBeVisible();
}

async function assertSummaryState(page: Page, state: SummaryState) {
  await openRiskPage(page);

  if (state === 'partial') {
    await expect(page.getByText('partial').first()).toBeVisible();
    await expect(page.getByText('Partial fixture warning')).toBeVisible();
    return;
  }

  if (state === 'stale') {
    await expect(page.getByText('stale').first()).toBeVisible();
    return;
  }

  if (state === 'blocked') {
    await expect(page.getByText('blocked').first()).toBeVisible();
    await expect(page.getByText('Sem score apresentavel')).toBeVisible();
    return;
  }

  if (state === 'null') {
    await expect(page.getByText('Sem score apresentavel')).toBeVisible();
    return;
  }

  await expect(page.getByText('Contexto desconhecido').first()).toBeVisible();
}

async function openRiskPage(page: Page) {
  await page.getByRole('button', { name: /^Risco e dados$/i }).click();
  await expect(page.getByRole('heading', { name: /Output de risco contextualizado/i })).toBeVisible();
}

function runtimeFailureMessage(page: Page, failure: SummaryFailure) {
  return failure === '500'
    ? page.getByText('Runtime summary failed')
    : page.getByText(/Failed to fetch|Runtime summary failed/i);
}

function allProtectedRequestsHaveBearer(requests: ObservedApiRequest[]) {
  return requests
    .filter(request =>
      request.path.startsWith('/control/runtime') ||
      request.path.startsWith('/dev/controlled-validation') ||
      request.path.startsWith('/control/simulation-runs'))
    .every(request => request.authorization?.startsWith('Bearer ') ?? false);
}
