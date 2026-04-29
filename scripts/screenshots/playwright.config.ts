import { defineConfig, devices } from '@playwright/test';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

/**
 * NineKgTools 截图任务 Playwright 配置
 *
 * - globalSetup 负责启动（或复用已启动的）NineKgTools.Web 并执行 API 登录，
 *   把 cookie 存到 auth.json（`storageState`）
 * - 所有 spec 默认复用该 storageState，跳过 UI 登录
 * - Blazor Server 首次建立 circuit 比较慢，这里把各项等待超时放宽
 */
export default defineConfig({
  testDir: './specs',
  outputDir: './test-results',

  // 串行执行：同一个 dotnet 后端不适合并发访问（会有状态耦合）
  fullyParallel: false,
  workers: 1,

  // 失败时不重试，避免重复调用后端导致状态不干净
  retries: 0,

  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
  ],

  // 较长的单用例超时：Blazor Server circuit 激活 + 首次数据加载可能需要 10+ 秒
  timeout: 90_000,
  expect: {
    timeout: 15_000,
  },

  globalSetup: path.resolve(__dirname, './global-setup.ts'),
  globalTeardown: path.resolve(__dirname, './global-teardown.ts'),

  use: {
    baseURL: process.env.NINEKG_BASE_URL ?? 'http://127.0.0.1:23333',
    // globalSetup 会把 cookie 写到这里
    storageState: path.resolve(__dirname, './auth.json'),

    viewport: { width: 1920, height: 1080 },
    deviceScaleFactor: 2, // Retina，README 里放大/缩小都清晰

    // Blazor Server 依赖 WebSocket（SignalR），请求失败自动重试
    ignoreHTTPSErrors: true,
    colorScheme: 'light',
    locale: 'zh-CN',
    timezoneId: 'Asia/Shanghai',

    // 截图风格
    screenshot: 'off',         // 我们在 spec 里手动截，关掉自动失败截图
    trace: 'retain-on-failure',
    video: 'off',

    // Development 模式下某些路由（overview / settings）SSR 比较慢，给足时间
    navigationTimeout: 90_000,
    actionTimeout: 15_000,
  },

  projects: [
    {
      name: 'chromium-desktop',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1920, height: 1080 }, deviceScaleFactor: 2 },
    },
  ],
});
