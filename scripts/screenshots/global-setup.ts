/**
 * Playwright globalSetup — 在所有 spec 执行前跑一次
 *
 * 流程：
 *   1. 如果 baseURL 已有服务（即用户本地 dotnet run 没停），直接复用
 *   2. 否则 spawn `dotnet run --project NineKgTools.Web`，等端口就绪
 *   3. 通过 POST /api/auth/login 拿 cookie，写入 auth.json（storageState）
 *   4. 把子进程 PID 存到 process.env.NINEKG_DOTNET_PID，供 teardown 使用
 */
import type { FullConfig } from '@playwright/test';
import { request } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { isAppRunning, startApp, stopApp, waitForReady } from './utils/app-lifecycle.ts';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const AUTH_STATE_PATH = path.resolve(__dirname, './auth.json');

export default async function globalSetup(_config: FullConfig): Promise<void> {
  const baseURL = process.env.NINEKG_BASE_URL ?? 'http://127.0.0.1:23333';
  const username = process.env.NT_USER ?? 'admin';
  const password = process.env.NT_PASSWORD ?? 'admin';

  console.log(`\n🎬 [globalSetup] 准备对 ${baseURL} 执行截图任务`);
  console.log(`🔑 [globalSetup] 登录凭据：${username} / ${'*'.repeat(password.length)}`);

  const alreadyRunning = await isAppRunning(baseURL, 3_000);
  let startedByUs = false;
  let childPid: number | undefined;

  if (alreadyRunning) {
    console.log('✅ [globalSetup] 检测到 baseURL 已有服务，直接复用');
  } else {
    console.log('🚀 [globalSetup] 未检测到服务，启动 dotnet run（首次可能需要 30-60 秒编译 & 迁移）…');
    const child = startApp(baseURL);
    startedByUs = true;
    childPid = child.pid;

    try {
      await waitForReady(baseURL, 180_000, 1_500);
      console.log('✅ [globalSetup] 服务已就绪');
    } catch (err) {
      // 启动失败，清理子进程
      try { await stopApp(child); } catch {}
      throw err;
    }
  }

  // 把子进程相关状态持久化，供 teardown 使用
  await fs.writeFile(
    path.resolve(__dirname, './.runtime-state.json'),
    JSON.stringify({ startedByUs, childPid, baseURL }, null, 2),
    'utf-8',
  );

  // 通过 API 登录，拿到 cookie
  console.log('🔑 [globalSetup] 执行 API 登录…');
  const apiContext = await request.newContext({ baseURL, ignoreHTTPSErrors: true });
  try {
    const response = await apiContext.post('/api/auth/login', {
      form: {
        username,
        password,
        rememberMe: 'true',
      },
      failOnStatusCode: false,
    });

    if (!response.ok()) {
      const body = await response.text();
      throw new Error(
        `登录失败：${response.status()} ${response.statusText()}。` +
          `响应体片段：${body.slice(0, 200)}。` +
          `请确认 admin/admin 凭据正确（或设置 NT_USER / NT_PASSWORD 环境变量）。`,
      );
    }

    await apiContext.storageState({ path: AUTH_STATE_PATH });
    console.log(`✅ [globalSetup] 登录成功，cookie 已写入 ${path.relative(process.cwd(), AUTH_STATE_PATH)}`);
  } finally {
    await apiContext.dispose();
  }
}
