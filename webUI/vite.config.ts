/// <reference types="vitest/config" />
import { defineConfig } from 'vitest/config';
import path from 'path';
import { fileURLToPath } from 'url';
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { Plugin } from 'vite';
import { execFile } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';


const __dirname = path.dirname(fileURLToPath(import.meta.url));
const apiProxyTarget = process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5254';

export default defineConfig({
  plugins: [react(), tailwindcss(), localTestRunner()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  assetsInclude: ['**/*.svg', '**/*.csv'],
  server: {
    proxy: {
      '/api': {
        target: apiProxyTarget,
        changeOrigin: true,
        secure: false,
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    include: ['src/**/*.test.{ts,tsx}'],
    setupFiles: ['./src/test/setup.ts'],
    reporters: ['default', 'junit'],
    outputFile: {
      junit: './test-results/vitest-junit.xml',
    },
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html', 'lcov', 'json-summary', 'cobertura'],
      reportsDirectory: './coverage',
      include: ['src/app/**/*.{ts,tsx}'],
      exclude: ['src/**/*.test.{ts,tsx}', 'src/test/**', 'src/main.tsx'],
    },
  },
});

function localTestRunner(): Plugin {
  return {
    name: 'local-test-runner',
    configureServer(server) {
      server.middlewares.use('/__local-run-tests', async (req, res) => {
        if (req.method !== 'POST') {
          res.statusCode = 405;
          res.end('Method not allowed');
          return;
        }

        const cwd = process.cwd();
        const scriptPath = join(cwd, '..', 'scripts', 'tests', 'run-all-tests.ps1');
        const resultsDir = join(cwd, 'testSuiteResults');

        res.setHeader('Content-Type', 'application/json');

        if (!existsSync(scriptPath)) {
          res.statusCode = 500;
          res.end(JSON.stringify({ error: `Script not found: ${scriptPath}` }));
          return;
        }

        console.log('[local-test-runner] Starting run-all-tests.ps1...');

        execFile('pwsh.exe', [
          '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', scriptPath
        ], { cwd: join(cwd, '..'), timeout: 15 * 60 * 1000 }, (err, stdout, stderr) => {
          if (err) {
            console.error('[local-test-runner] Error:', err.message);
          }
          if (stderr) {
            console.error('[local-test-runner] stderr:', stderr);
          }
          if (stdout) {
            console.log('[local-test-runner] stdout (last 1k):', stdout.slice(-1000));
          }

          const summaryPath = join(resultsDir, '_summary.json');
          if (existsSync(summaryPath)) {
            const summary = readFileSync(summaryPath, 'utf-8');
            res.end(summary);
          } else {
            const errorDetails = err
              ? `${err.message}${stderr ? '\n' + stderr : ''}`
              : 'Script completed but no _summary.json generated';
            res.statusCode = 500;
            res.end(JSON.stringify({ error: 'Test run failed', details: errorDetails }));
          }
        });
      });
    },
  };
}