import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL: 'http://localhost:5174',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    ...devices['Desktop Chrome'],
    channel: process.platform === 'win32' ? 'msedge' : undefined,
  },
  webServer: [
    {
      command: 'dotnet ../Backend/BlueHarvest.PricingExceptions/src/PricingExceptions.API/bin/Debug/net10.0/PricingExceptions.API.dll --urls http://localhost:5270',
      env: { ASPNETCORE_ENVIRONMENT: 'Development', Database__Provider: 'InMemory' },
      url: 'http://localhost:5270/openapi/v1.json',
      timeout: 60_000,
      reuseExistingServer: !process.env.CI,
    },
    {
      command: 'node node_modules/vite/bin/vite.js --host localhost --port 5174 --strictPort',
      url: 'http://localhost:5174',
      env: { API_PROXY_TARGET: 'http://localhost:5270' },
      timeout: 30_000,
      reuseExistingServer: false,
    },
  ],
})
