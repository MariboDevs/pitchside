import { defineConfig, devices } from '@playwright/test';

// Starts the API (in-memory, scripted crew) and the Angular dev server, then drives every surface in Chromium.
// PW_CHROMIUM_PATH points at a pre-installed Chromium when the bundled one is not downloaded.
const executablePath = process.env['PW_CHROMIUM_PATH'];

export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  fullyParallel: false,
  workers: 1,
  reporter: [['list']],
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'retain-on-failure',
    ...devices['Desktop Chrome'],
    launchOptions: executablePath ? { executablePath } : {},
  },
  webServer: [
    {
      command:
        'dotnet run --project ../src/Pitchside.Api --no-launch-profile --urls http://localhost:5180',
      url: 'http://localhost:5180/api/v1/clubs',
      reuseExistingServer: !process.env['CI'],
      timeout: 180_000,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        Agents__Provider: 'Scripted',
        Storage__Provider: 'InMemory',
        Bus__Provider: 'InProcess',
      },
    },
    {
      command: 'npx ng serve --port 4200',
      url: 'http://localhost:4200',
      reuseExistingServer: !process.env['CI'],
      timeout: 180_000,
    },
  ],
});
