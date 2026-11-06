import { defineConfig, devices } from '@playwright/test'

const externalBaseUrl = process.env.E2E_BASE_URL?.trim() || undefined
const frontendUrl = 'http://localhost:5174'
const backendUrl = 'http://localhost:5270'

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL: externalBaseUrl ?? frontendUrl,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    ...devices['Desktop Chrome'],
    channel: process.platform === 'win32' ? 'msedge' : undefined,
  },
  // An explicit URL tests an already running deployment without starting isolated servers.
  webServer: externalBaseUrl ? undefined : [
    {
      // Build the current source rather than depending on a previously built DLL.
      command: `dotnet run --project ../Backend/BlueHarvest.PricingExceptions/src/PricingExceptions.API/PricingExceptions.API.csproj --no-launch-profile --urls ${backendUrl}`,
      env: { ASPNETCORE_ENVIRONMENT: 'Development', Database__Provider: 'InMemory' },
      url: `${backendUrl}/openapi/v1.json`,
      timeout: 120_000,
      reuseExistingServer: false,
    },
    {
      command: 'node node_modules/vite/bin/vite.js --host localhost --port 5174 --strictPort',
      url: frontendUrl,
      env: { API_PROXY_TARGET: backendUrl },
      timeout: 30_000,
      reuseExistingServer: false,
    },
  ],
})
