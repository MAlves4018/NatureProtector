import AxeBuilder from '@axe-core/playwright';
import { expect, request, test, type APIRequestContext, type Page, type TestInfo } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import fs from 'node:fs/promises';
import path from 'node:path';

interface RoleJourney {
  role: string;
  useExistingAdministrator: boolean;
  allowedRoute: string;
  allowedHeading: string;
  requiredCapabilities: string[];
  forbiddenCapabilities: string[];
  deniedRoute: string | null;
}

interface AcceptanceConfig {
  runtime: {
    apiBaseUrl: string;
    adminUsernameEnvironmentVariable: string;
    adminPasswordEnvironmentVariable: string;
    defaultAdminUsername: string;
    defaultAdminPassword: string;
    temporaryPassword: string;
    temporaryUsernamePrefix: string;
    temporaryEmailDomain: string;
    temporaryOrganization: string;
  };
  playwright: {
    accessibilityBlockedImpacts: string[];
    roles: RoleJourney[];
  };
}

interface Credentials {
  username: string;
  password: string;
  userId?: string;
}

const configPath = process.env.NP_UI_ACCEPTANCE_CONFIG
  ? path.resolve(process.env.NP_UI_ACCEPTANCE_CONFIG)
  : path.resolve(process.cwd(), '../config/acceptance/ui-performance-coverage.json');
const config = JSON.parse(await fs.readFile(configPath, 'utf8')) as AcceptanceConfig;
const screenshotRoot = process.env.UI_REVISION_SCREENSHOTS;
const credentials = new Map<string, Credentials>();
const createdUserIds: string[] = [];
let api: APIRequestContext;
let adminToken = '';

const administrator = {
  username:
    process.env[config.runtime.adminUsernameEnvironmentVariable] ?? config.runtime.defaultAdminUsername,
  password:
    process.env[config.runtime.adminPasswordEnvironmentVariable] ?? config.runtime.defaultAdminPassword,
};

test.describe.serial('live role journeys and accessibility', () => {
  test.skip(process.env.LIVE_RUNTIME !== '1', 'Requires the official local runtime launcher.');

  test.beforeAll(async () => {
    api = await request.newContext({ baseURL: process.env.NP_UI_API_BASE_URL ?? config.runtime.apiBaseUrl });
    const login = await api.post('/api/users-roles/login', {
      data: { usernameOrEmail: administrator.username, password: administrator.password },
    });
    expect(login.status(), await login.text()).toBe(200);
    adminToken = String((await login.json()).token ?? '');
    expect(adminToken).not.toBe('');
    credentials.set('Admin', administrator);

    const rolesResponse = await api.get('/api/users-roles/roles', { headers: bearer(adminToken) });
    expect(rolesResponse.status(), await rolesResponse.text()).toBe(200);
    const roles = (await rolesResponse.json()) as Array<{ id: number; name: string }>;
    const roleByName = new Map(roles.map((role) => [role.name, role.id]));
    const suffix = `${Date.now()}-${randomUUID().slice(0, 8)}`;

    for (const journey of config.playwright.roles.filter((item) => !item.useExistingAdministrator)) {
      const roleId = roleByName.get(journey.role);
      expect(roleId, `Seeded role missing: ${journey.role}`).toBeDefined();
      const slug = journey.role.toLowerCase().replace(/[^a-z0-9]+/g, '-');
      const username = `${config.runtime.temporaryUsernamePrefix}-${slug}-${suffix}`;
      const email = `${username}@${config.runtime.temporaryEmailDomain}`;
      const create = await api.post('/api/users-roles/users', {
        headers: bearer(adminToken),
        data: {
          username,
          password: config.runtime.temporaryPassword,
          email,
          organization: config.runtime.temporaryOrganization,
          roles: [],
        },
      });
      expect(create.status(), await create.text()).toBe(200);
      const userId = String((await create.json()).id ?? '');
      expect(userId).not.toBe('');
      createdUserIds.push(userId);

      const assign = await api.put(`/api/users-roles/users/${userId}/roles/${roleId}`, {
        headers: bearer(adminToken),
      });
      expect(assign.status(), await assign.text()).toBe(200);
      credentials.set(journey.role, { username, password: config.runtime.temporaryPassword, userId });
    }
  });

  test.afterAll(async () => {
    if (api) {
      for (const userId of createdUserIds.reverse()) {
        await api.delete(`/api/users-roles/users/${userId}`, { headers: bearer(adminToken) }).catch(() => null);
      }
      if (adminToken) {
        await api.post('/api/users-roles/logout', { headers: bearer(adminToken) }).catch(() => null);
      }
      await api.dispose();
    }
  });

  test('public surface stays bounded on the live runtime', async ({ page }, testInfo) => {
    const observer = observePage(page);
    await page.goto('/demo');
    await expect(page.getByRole('heading', { name: 'NatureProtector' }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: /entrar/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Pipeline$/i })).toHaveCount(0);
    await assertAccessibility(page, 'Public');
    await capture(page, testInfo, 'public-live');
    observer.assertClean();
  });

  for (const journey of config.playwright.roles) {
    test(`${journey.role} receives only its live UI authority`, async ({ page }, testInfo) => {
      const observer = observePage(page);
      const identity = credentials.get(journey.role);
      expect(identity, `Credentials not prepared for ${journey.role}`).toBeDefined();
      await login(page, identity!);

      const capabilityProfile = await page.evaluate(async () => {
        const response = await fetch('/api/users-roles/me/capabilities', {
          headers: { Authorization: `Bearer ${localStorage.getItem('token') ?? ''}` },
        });
        if (!response.ok) throw new Error(`Capability profile returned HTTP ${response.status}`);
        return (await response.json()) as { capabilities: string[]; authority: string };
      });
      for (const capability of journey.requiredCapabilities) {
        expect(capabilityProfile.capabilities, `${journey.role} missing ${capability}`).toContain(capability);
      }
      for (const capability of journey.forbiddenCapabilities) {
        expect(capabilityProfile.capabilities, `${journey.role} unexpectedly has ${capability}`).not.toContain(
          capability,
        );
      }

      await page.goto(journey.allowedRoute);
      await expect(page.getByRole('heading', { name: journey.allowedHeading })).toBeVisible({ timeout: 30_000 });
      await assertAccessibility(page, journey.role);
      await capture(page, testInfo, `${journey.role.toLowerCase()}-allowed`);

      if (journey.deniedRoute) {
        await page.goto(journey.deniedRoute);
        await expect(page.getByRole('heading', { name: 'Acesso negado' })).toBeVisible({ timeout: 20_000 });
        await expect(page.getByText(/capabilities confirmadas pelo backend/i)).toBeVisible();
        await capture(page, testInfo, `${journey.role.toLowerCase()}-denied`);
      }

      observer.assertClean();
      await logout(page);
    });
  }
});

function bearer(token: string) {
  return { Authorization: `Bearer ${token}` };
}

async function login(page: Page, identity: Credentials) {
  await page.goto('/login');
  await page.getByLabel('Username or email').fill(identity.username);
  await page.getByLabel('Password').fill(identity.password);
  await page.getByRole('button', { name: /^Sign in$/ }).click();
  await expect.poll(() => page.evaluate(() => localStorage.getItem('token'))).not.toBeNull();
  await expect(page.getByLabel('Navegação principal')).toBeVisible({ timeout: 30_000 });
}

async function logout(page: Page) {
  const accountButton = page.getByRole('button', { name: /Signed in/i }).first();
  if (await accountButton.isVisible().catch(() => false)) await accountButton.click();
  const signOut = page.getByRole('button', { name: /Logout|Sign out|Sair/i });
  if (await signOut.isVisible().catch(() => false)) {
    await signOut.click();
  } else {
    await page.evaluate(() => {
      localStorage.removeItem('token');
      sessionStorage.clear();
    });
    await page.goto('/login');
  }
  await expect.poll(() => page.evaluate(() => localStorage.getItem('token'))).toBeNull();
}

async function assertAccessibility(page: Page, role: string) {
  const result = await new AxeBuilder({ page }).analyze();
  const blocked = new Set(config.playwright.accessibilityBlockedImpacts);
  const violations = result.violations.filter((violation) => blocked.has(violation.impact ?? ''));
  expect(violations, `${role} accessibility violations: ${JSON.stringify(violations)}`).toEqual([]);
}

function observePage(page: Page) {
  const serverErrors: string[] = [];
  const consoleErrors: string[] = [];
  page.on('response', (response) => {
    if (response.status() >= 500) serverErrors.push(`${response.status()} ${response.url()}`);
  });
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });
  page.on('pageerror', (error) => consoleErrors.push(error.message));
  return {
    assertClean: () => {
      expect(serverErrors, `Unexpected HTTP 5xx responses: ${serverErrors.join('; ')}`).toEqual([]);
      expect(consoleErrors, `Unexpected browser errors: ${consoleErrors.join('; ')}`).toEqual([]);
    },
  };
}

async function capture(page: Page, testInfo: TestInfo, name: string) {
  if (!screenshotRoot) return;
  await fs.mkdir(screenshotRoot, { recursive: true });
  await page.screenshot({
    path: path.join(screenshotRoot, `${name}-${testInfo.project.name}.png`),
    fullPage: true,
  });
}
