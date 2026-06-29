import { expect, test, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const axeSource = readFileSync(require.resolve('axe-core/axe.min.js'), 'utf8');

const areaFixture = {
  id: 'area-001',
  code: 'proenca-a-nova',
  name: 'Proenca-a-Nova',
  countryCode: 'PT',
  configurationVersionNumber: 1,
  gridCellCount: 12,
  sensorNodeCount: 2,
  scenarioCount: 2,
};

test.describe('UI v2 public surface', () => {
  test.beforeEach(async ({ page }) => {
    await mockApi(page);
  });

  test('loads the public product surface without protected navigation or axe violations', async ({ page }) => {
    await page.goto('/ui-v2');

    await expect(page.getByRole('heading', { name: 'NatureProtector UI v2' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'NatureProtector', level: 2 })).toBeVisible();
    await expect(page.getByText('Data Status')).toBeVisible();
    await expect(page.getByRole('link', { name: /entrar/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Pipeline$/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^Simulacao$/i })).toHaveCount(0);

    await expectAxeClean(page);
  });

  test('supports keyboard focus, help dialog lifecycle, dark mode and reduced motion', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/ui-v2');

    const skipLink = page.getByRole('link', { name: /Saltar para o conteudo|Saltar para o conteúdo|Skip to content/i });
    await skipLink.focus();
    await expect(skipLink).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(page.locator('#ui-v2-main')).toBeFocused();

    const englishButton = page.getByRole('button', { name: /^EN$/ });
    await englishButton.focus();
    await page.keyboard.press('F1');

    const dialog = page.getByRole('dialog', { name: /Ajuda contextual|Contextual help/i });
    await expect(dialog).toBeVisible();
    await expect(page.getByRole('button', { name: /Fechar ajuda|Close help/i })).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(page.getByRole('button', { name: /Fechar ajuda|Close help/i })).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);
    await expect(englishButton).toBeFocused();

    await page.getByRole('button', { name: /^Dark$/i }).click();
    await expect(page.locator('.ui-v2-shell')).toHaveAttribute('data-theme', 'dark');
    await expect(page.getByRole('banner')).toBeVisible();
    await expectAxeClean(page);
  });
});

async function expectAxeClean(page: Page) {
  await page.addScriptTag({ content: axeSource });
  const violations = await page.evaluate(async () => {
    const axe = (
      window as unknown as {
        axe: {
          run: (context: Document) => Promise<{
            violations: Array<{
              id: string;
              impact: string | null;
              help: string;
              nodes: Array<{ target: string[] }>;
            }>;
          }>;
        };
      }
    ).axe;
    const result = await axe.run(document);

    return result.violations.map((violation) => ({
      id: violation.id,
      impact: violation.impact,
      help: violation.help,
      targets: violation.nodes.flatMap((node) => node.target),
    }));
  });

  expect(violations, JSON.stringify(violations, null, 2)).toEqual([]);
}

async function mockApi(page: Page) {
  await page.route('**/api/**', async (route) => {
    const url = new URL(route.request().url());

    if (url.pathname === '/api/control/areas') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([areaFixture]),
      });
      return;
    }

    await route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ message: `Unhandled E2E API route: ${url.pathname}` }),
    });
  });
}
