import { expect, test, type Page } from '@playwright/test';
import {
  installUiV2ApiFixture,
  type ObservedApiRequest,
  type RoleProfile,
  type SummaryFailure,
  type SummaryState,
} from './ui-v2-api-fixture';

test.describe('UI v2 authenticated role flows', () => {
  test('anonymous users see landing, login, and no protected operations', async ({ page }) => {
    await installUiV2ApiFixture(page);

    await page.goto('/ui-v2');

    await expect(page.getByRole('heading', { name: 'NatureProtector', level: 1 })).toBeVisible();
    await expect(page.getByRole('button', { name: /entrar/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Pipeline$/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^Simulação$/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^Administração$/i })).toHaveCount(0);

    await page.goto('/ui-v2?area=proenca-a-nova');
    await expect(page.getByRole('button', { name: /^Pipeline$/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^P3 experimental$/i })).toHaveCount(0);
  });

  test('Admin login can navigate health, evidence, and proportional administration', async ({ page }) => {
    const api = await installUiV2ApiFixture(page, { profile: 'Admin' });

    await signIn(page, 'Admin');

    await expect(page.getByText('Perfil ativo: Admin')).toBeVisible();

    await openNavPage(page, /^Técnico$/i, /^Pipeline$/i);
    await expect(page.getByRole('heading', { name: /Pipeline e observabilidade/i })).toBeVisible();
    await page.getByRole('button', { name: /Runtime current state/i }).click();
    await expect(page.getByText('Prevention.Host health')).toHaveCount(1);
    await expect(page.getByText('RabbitMQ health')).toHaveCount(1);
    await expect(page.getByText('Ingestion ready')).toHaveCount(1);

    await openNavPage(page, /^Técnico$/i, /^Qualidade e evidencia$/i);
    await expect(page.getByRole('heading', { name: /Qualidade e evidência/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Runtime smoke evidence' })).toBeVisible();
    const download = page.waitForEvent('download');
    await page.getByRole('button', { name: /Download evidence/i }).click();
    expect((await download).suggestedFilename()).toBe('ui-v2-runtime-smoke.txt');

    await openNavPage(page, /^Admin$/i, /^Administração$/i);
    await expect(page.getByRole('heading', { name: /Administração proporcional/i })).toBeVisible();
    await expect(page.getByText('Runtime reset')).toBeVisible();
    await expect(page.getByText('User/role administration')).toBeVisible();

    expect(api.requests.some((request) => request.path === '/control/runtime/observability/health')).toBe(true);
    expect(api.requests.some((request) => request.path === '/control/runtime/observability/evidence')).toBe(true);
    expect(allProtectedRequestsHaveBearer(api.requests)).toBe(true);
  });

  test('Sim login can validate and start a degraded runtime run without pipeline access', async ({ page }) => {
    const api = await installUiV2ApiFixture(page, { profile: 'Sim' });

    await signIn(page, 'Sim');

    await openNavPage(page, /^Simulações$/i, /^Simulação$/i);
    await expect(page.getByRole('button', { name: /^Pipeline$/i })).toHaveCount(0);

    await page.getByLabel(/Selecionar cenário/i).selectOption('scenario_c');
    await page.getByRole('combobox', { name: /^Degradação$/i }).selectOption('power-degradation');
    await expect(page.getByRole('button', { name: /Iniciar simulação/i })).toBeEnabled();
    await page.getByRole('button', { name: /Iniciar simulação/i }).click();

    await expect(page.getByRole('heading', { name: /Contexto da execução/i })).toBeVisible();
    const startRequest = api.requests.find(
      (request) => request.method === 'POST' && request.path === '/control/runtime/runs',
    );
    expect(startRequest?.authorization).toBe('Bearer sim-token');
    expect(startRequest?.postData).toMatchObject({
      areaCode: 'proenca-a-nova',
      scenarioCode: 'scenario_c',
      degradationProfile: 'power-degradation',
      degradationProfiles: ['power-degradation'],
    });
  });

  test('Pipeline login sees pipeline health, queues and audit without simulation access', async ({ page }) => {
    const api = await installUiV2ApiFixture(page, { profile: 'Pipeline' });

    await signIn(page, 'Pipeline');

    await openNavPage(page, /^Técnico$/i, /^Pipeline$/i);
    await expect(page.getByRole('button', { name: /^Qualidade e evidencia$/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Simulação$/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^Administração$/i })).toHaveCount(0);

    await page.getByRole('button', { name: /Runtime current state/i }).click();
    await expect(page.getByText('Queue state')).toHaveCount(1);
    await expect(page.getByText('Ingestion unacknowledged')).toHaveCount(1);

    await openNavPage(page, /^Simulações$/i, /^Execuções$/i);
    await expect(page.getByRole('heading', { name: /Contexto da execução/i })).toBeVisible();
    await expect(page.getByRole('heading', { name: /Auditoria da execução/i })).toBeVisible();
    await expect(page.getByText('Sem evidência').first()).toBeVisible();

    expect(api.requests.some((request) => request.path === '/control/runtime/observability/rabbitmq')).toBe(true);
    expect(api.requests.some((request) => request.path === '/control/runtime/runs')).toBe(false);
  });

  for (const profile of ['Admin', 'Sim', 'Pipeline'] as const) {
    test(`${profile} login survives browser reload with the same role surface`, async ({ page }) => {
      await installUiV2ApiFixture(page, { profile });

      await signIn(page, profile);
      await expect(page.getByText(`Perfil ativo: ${profile}`)).toBeVisible();

      await page.reload();

      await expect(page.getByText(`Perfil ativo: ${profile}`)).toBeVisible();
      await expect
        .poll(() => page.evaluate(() => localStorage.getItem('token')))
        .toBe(`${profile.toLowerCase()}-token`);
      await expect(page.getByRole('button', { name: /entrar/i })).toHaveCount(0);

      if (profile === 'Admin') {
        await openNavPage(page, /^Admin$/i, /^Administração$/i);
        await expect(page.getByRole('heading', { name: /Administração proporcional/i })).toBeVisible();
      } else if (profile === 'Sim') {
        await openNavPage(page, /^Simulações$/i, /^Simulação$/i);
        await expect(page.getByRole('heading', { name: /^Simulação$/i })).toBeVisible();
        await expect(page.getByRole('button', { name: /^Pipeline$/i })).toHaveCount(0);
      } else {
        await openNavPage(page, /^Técnico$/i, /^Pipeline$/i);
        await expect(page.getByRole('heading', { name: /Pipeline e observabilidade/i })).toBeVisible();
        await expect(page.getByRole('button', { name: /^Simulação$/i })).toHaveCount(0);
      }
    });
  }
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

    await expect(page.getByRole('button', { name: /entrar/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Pipeline$/i })).toHaveCount(0);
    await expect.poll(() => page.evaluate(() => localStorage.getItem('token'))).toBeNull();
  });

  test('invalid stored token is removed before exposing authenticated UI v2 surfaces', async ({ page }) => {
    await installUiV2ApiFixture(page, { profile: 'Admin' });
    await page.addInitScript(() => localStorage.setItem('token', 'invalid-token'));

    await page.goto('/ui-v2?area=proenca-a-nova');

    await expect(page.getByRole('button', { name: /entrar/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Administração$/i })).toHaveCount(0);
    await expect.poll(() => page.evaluate(() => localStorage.getItem('token'))).toBeNull();
  });

  test('unknown roles stay on public-only capability surface after login', async ({ page }) => {
    await installUiV2ApiFixture(page, { profile: 'Unknown' });

    await signIn(page, 'Unknown');

    await expect(page.getByText('Perfil ativo: Observer')).toBeVisible();
    await expect(page.getByRole('button', { name: /^Público$/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Pipeline$/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^Simulação$/i })).toHaveCount(0);
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
    await openNavPage(page, /^Simulações$/i, /^Simulação$/i);
    await page.getByLabel(/Selecionar cenário/i).selectOption('scenario_b');
    await page.getByRole('button', { name: /Iniciar simulação/i }).click();

    await expect(page.getByText('Forbidden by mock RBAC')).toBeVisible();
  });
});

async function signIn(page: Page, profile: RoleProfile) {
  await page.goto('/ui-v2');
  await page.goto('/login');
  await page.getByLabel(/Username or email/i).fill(`${profile.toLowerCase()}@natureprotector.test`);
  await page.getByLabel(/Password/i).fill('password');
  const loginResponse = page.waitForResponse(
    (response) => response.url().includes('/api/users-roles/login') && response.request().method() === 'POST',
  );
  await page.getByRole('button', { name: /^Sign in$/i }).click();
  await expect((await loginResponse).status()).toBe(200);
  await expect(page).toHaveURL(/\/ui-v2/);
  await expect.poll(() => page.evaluate(() => localStorage.getItem('token'))).toBe(`${profile.toLowerCase()}-token`);
  await page.goto('/ui-v2?area=proenca-a-nova');
  await expect(page.getByRole('heading', { name: 'NatureProtector', level: 1 })).toBeVisible();
}

async function openNavPage(page: Page, groupName: RegExp, pageName: RegExp) {
  await page.getByRole('button', { name: groupName }).click();
  const pageButton = page.getByRole('button', { name: pageName });
  try {
    await expect(pageButton).toBeVisible({ timeout: 1_000 });
    await pageButton.click();
  } catch {
    // Some groups contain a single page, so clicking the group is the navigation action.
  }
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
    await expect(page.getByText('Sem score apresentável')).toBeVisible();
    return;
  }

  if (state === 'null') {
    await expect(page.getByText('Sem score apresentável')).toBeVisible();
    return;
  }

  await expect(page.getByText('Contexto desconhecido').first()).toBeVisible();
}

async function openRiskPage(page: Page) {
  await openNavPage(page, /^Operar$/i, /^Risco e dados$/i);
}

function runtimeFailureMessage(page: Page, failure: SummaryFailure) {
  return failure === '500'
    ? page.getByText('Runtime summary failed')
    : page.getByText(/Failed to fetch|Runtime summary failed/i);
}

function allProtectedRequestsHaveBearer(requests: ObservedApiRequest[]) {
  return requests
    .filter(
      (request) =>
        request.path.startsWith('/control/runtime') ||
        request.path.startsWith('/dev/controlled-validation') ||
        request.path.startsWith('/control/simulation-runs'),
    )
    .every((request) => request.authorization?.startsWith('Bearer ') ?? false);
}
