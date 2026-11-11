import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './tests/browser',
  fullyParallel: false,
  workers: 1,
  timeout: 60000,
  expect: { timeout: 10000 },
  outputDir: 'artifacts/browser-results',
  reporter: [['list'], ['html', {outputFolder:'artifacts/browser-report',open:'never'}]],
  use: { baseURL:'http://127.0.0.1:5280', channel:'msedge', headless:true, viewport:{width:1520,height:1000}, screenshot:'only-on-failure', trace:'retain-on-failure' },
  // Historical browser regression suite, not the default/native game client.
  webServer: { command:'artifacts\\dotnet\\dotnet.exe run --project src/InfiniteDao.LegacyWeb --configuration Release', url:'http://127.0.0.1:5280/api/catalog', reuseExistingServer:true, timeout:120000 }
});
