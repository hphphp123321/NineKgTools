/**
 * NineKgTools.Web 子进程生命周期管理
 *
 * - isAppRunning：HTTP 探测 baseURL 是否已经有服务
 * - startAppIfNeeded：没有则用 `dotnet run` 启动，返回 ChildProcess（由 teardown 关闭）
 * - waitForReady：轮询直到 /login 返回 2xx/3xx
 */
import { spawn, type ChildProcess } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, '../../..');

export interface AppHandle {
  /** 如果本次是我们启动的子进程，保存句柄便于 teardown 关闭；复用已有服务时为 undefined */
  process?: ChildProcess;
  /** 实际使用的 baseURL */
  baseURL: string;
  /** 本次是否由我们启动（决定 teardown 行为） */
  startedByUs: boolean;
}

/**
 * 检查 baseURL 是否已有服务。任何能握手 HTTP 的响应都算"已就绪"。
 */
export async function isAppRunning(baseURL: string, timeoutMs = 2_000): Promise<boolean> {
  try {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs);
    const response = await fetch(`${baseURL}/login`, {
      method: 'GET',
      signal: controller.signal,
      redirect: 'manual',
    });
    clearTimeout(timer);
    // 2xx/3xx/401 都说明服务在响应
    return response.status < 500;
  } catch {
    return false;
  }
}

/**
 * 轮询直到应用就绪或超时。
 */
export async function waitForReady(baseURL: string, maxWaitMs = 120_000, intervalMs = 1_000): Promise<void> {
  const deadline = Date.now() + maxWaitMs;
  let lastError: unknown = null;

  while (Date.now() < deadline) {
    if (await isAppRunning(baseURL, 2_000)) {
      return;
    }
    await sleep(intervalMs);
  }

  throw new Error(
    `等待 NineKgTools.Web 就绪超时（${maxWaitMs}ms，baseURL=${baseURL}）。` +
      `最近的错误：${lastError ?? '未知'}`,
  );
}

/**
 * 启动 NineKgTools.Web 子进程。仅在 isAppRunning 为 false 时调用。
 */
export function startApp(baseURL: string): ChildProcess {
  const isWindows = process.platform === 'win32';
  const dotnet = isWindows ? 'dotnet.exe' : 'dotnet';

  const args = [
    'run',
    '--project',
    'NineKgTools.Web',
    '--no-launch-profile',
    '--',
    '--urls',
    baseURL,
  ];

  const child = spawn(dotnet, args, {
    cwd: REPO_ROOT,
    env: {
      ...process.env,
      // Development 环境下 UseStaticWebAssets() 自动启用，
      // _content/MudBlazor/MudBlazor.min.css 等才能被服务出去
      ASPNETCORE_ENVIRONMENT: 'Development',
      DOTNET_CLI_TELEMETRY_OPTOUT: '1',
      DOTNET_NOLOGO: '1',
    },
    stdio: ['ignore', 'pipe', 'pipe'],
    shell: false,
    windowsHide: true,
    detached: false,
  });

  // 把子进程输出前缀一下转发，方便诊断启动失败
  child.stdout?.on('data', (chunk: Buffer) => {
    process.stdout.write(`[dotnet] ${chunk.toString()}`);
  });
  child.stderr?.on('data', (chunk: Buffer) => {
    process.stderr.write(`[dotnet:err] ${chunk.toString()}`);
  });
  child.on('error', (err) => {
    console.error('[dotnet] 子进程出错：', err);
  });

  return child;
}

/**
 * 优雅关闭子进程。Windows 下 .NET host 对信号响应有限，先 SIGTERM 再必要时 kill。
 */
export async function stopApp(child: ChildProcess, graceMs = 8_000): Promise<void> {
  if (child.killed || child.exitCode !== null) return;

  const killed = new Promise<void>((resolve) => {
    child.once('exit', () => resolve());
  });

  // 先尝试优雅关闭
  if (process.platform === 'win32') {
    // Windows 下 taskkill /T 终止进程树
    const { exec } = await import('node:child_process');
    exec(`taskkill /pid ${child.pid} /T /F`, () => {});
  } else {
    child.kill('SIGTERM');
  }

  await Promise.race([killed, sleep(graceMs)]);

  if (!child.killed && child.exitCode === null) {
    child.kill('SIGKILL');
  }
}

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}
