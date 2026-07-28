import { defineConfig, devices } from '@playwright/test';
import path from 'node:path';

const externalRoot = process.env.UI_REVISION_RUNS;
const sensitiveAcceptance = process.env.NP_UI_SENSITIVE_ACCEPTANCE === '1';

export default defineConfig({
  testDir: './e2e',
  timeout: 45_000,
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  reporter: [
    ['list'],
    [
      'html',
      {
        outputFolder: externalRoot ? path.join(externalRoot, 'playwright-report') : 'playwright-report',
        open: 'never',
      },
    ],
  ],
  outputDir: externalRoot ? path.join(externalRoot, 'playwright-artifacts') : 'test-results/playwright',
  use: {
    baseURL: process.env.LIVE_RUNTIME === '1' ? 'http://127.0.0.1:5173' : 'http://127.0.0.1:4173',
    extraHTTPHeaders: process.env.NP_EVIDENCE_RUN_ID
      ? { 'X-NP-Evidence-Run-Id': process.env.NP_EVIDENCE_RUN_ID }
      : undefined,
    trace: sensitiveAcceptance ? 'off' : 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: sensitiveAcceptance ? 'off' : 'retain-on-failure',
  },
  webServer:
    process.env.LIVE_RUNTIME === '1'
      ? undefined
      : {
          command: 'npm run dev -- --host 127.0.0.1 --port 4173 --strictPort',
          url: 'http://127.0.0.1:4173',
          reuseExistingServer: true,
          timeout: 120_000,
        },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'], viewport: { width: 1920, height: 1080 } } },
    { name: 'desktop-1536', use: { ...devices['Desktop Chrome'], viewport: { width: 1536, height: 864 } } },
    { name: 'laptop', use: { ...devices['Desktop Chrome'], viewport: { width: 1366, height: 768 } } },
    { name: 'compact', use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 720 } } },
    { name: 'narrow', use: { ...devices['Pixel 7'] } },
  ],
});
