/**
 * Playwright globalTeardown — 在所有 spec 执行完后跑一次
 *
 * 如果 globalSetup 是我们启动的 dotnet 子进程，这里 kill 它；
 * 用户本地已经开着的服务不会被动。
 */
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

interface RuntimeState {
  startedByUs: boolean;
  childPid?: number;
  baseURL: string;
}

export default async function globalTeardown(): Promise<void> {
  const statePath = path.resolve(__dirname, './.runtime-state.json');
  let state: RuntimeState | null = null;
  try {
    state = JSON.parse(await fs.readFile(statePath, 'utf-8')) as RuntimeState;
  } catch {
    console.log('ℹ️  [globalTeardown] 未找到 runtime state，跳过清理');
    return;
  }

  if (!state.startedByUs || !state.childPid) {
    console.log('ℹ️  [globalTeardown] 本次复用的现有服务，不关闭');
    return;
  }

  console.log(`🛑 [globalTeardown] 关闭我们启动的 dotnet 子进程 (pid=${state.childPid})`);

  try {
    if (process.platform === 'win32') {
      const { exec } = await import('node:child_process');
      await new Promise<void>((resolve) => {
        exec(`taskkill /pid ${state!.childPid} /T /F`, () => resolve());
      });
    } else {
      try {
        process.kill(state.childPid, 'SIGTERM');
      } catch {
        // 进程可能已经退出
      }
    }
  } catch (err) {
    console.warn('⚠️  [globalTeardown] 关闭失败：', err);
  }

  // 清理临时状态文件
  try {
    await fs.unlink(statePath);
  } catch {
    // 忽略
  }
}
