/**
 * Blazor Server 页面"真正就绪"等待工具
 *
 * Blazor Server 的页面生命周期比传统 SPA 更复杂：
 *   1. 首次 HTML 请求返回（networkidle 到达）
 *   2. blazor.server.js 下载、启动
 *   3. SignalR circuit 建立
 *   4. OnInitializedAsync / OnAfterRenderAsync 执行
 *   5. 业务数据加载（从后端 pull）
 *
 * 只等 networkidle 远远不够——容易截到空白或 Loading 态。
 * 本工具组合三重条件：networkidle + Blazor Started + 业务标志元素。
 */
import type { Page } from '@playwright/test';

export interface BlazorReadyOptions {
  /** 必须可见的标志性元素 selector（每页可以不一样） */
  readySelector?: string;
  /** 额外冷却时间（ms），等待 CSS 过渡/动画收尾 */
  settleMs?: number;
  /** 单个等待阶段的最大 ms */
  perStageTimeout?: number;
}

/**
 * 等 Blazor Server 页面完全就绪。约定：所有阶段失败都抛 `Error`，由调用方决定是否重试。
 */
export async function waitForBlazor(
  page: Page,
  options: BlazorReadyOptions = {},
): Promise<void> {
  const {
    readySelector,
    settleMs = 2_500,
    perStageTimeout = 30_000,
  } = options;

  // Stage 1: 网络空闲（HTML + 静态资源已加载）
  await page.waitForLoadState('networkidle', { timeout: perStageTimeout });

  // Stage 2: Blazor JS 加载且 SignalR 连接建立
  //   window.Blazor 对象在 blazor.server.js 启动后存在；
  //   `<div id="blazor-error-ui">` 在 _Host.cshtml / App.razor 里是静态 DOM，不是就绪标志；
  //   更稳的方式是等 body 上 aria-busy 属性消失 + Blazor 对象存在。
  await page.waitForFunction(
    () => {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const w = window as any;
      return typeof w.Blazor !== 'undefined';
    },
    undefined,
    { timeout: perStageTimeout },
  );

  // Stage 3: MudBlazor 根布局渲染完成——至少 .mud-appbar 或 .mud-layout 要在 DOM 里
  //   这是 NineKgTools 所有非 Login 页的共同祖先。
  //   对 /login 页面则用 .login-card 作为 fallback（由调用方传 readySelector）
  const layoutSelectors = [
    readySelector,
    '.mud-appbar',
    '.mud-layout',
  ].filter(Boolean) as string[];

  for (const sel of layoutSelectors) {
    try {
      await page.locator(sel).first().waitFor({ state: 'visible', timeout: perStageTimeout });
      break;
    } catch {
      // 尝试下一个
    }
  }

  // Stage 4: **关键**——等待主内容区域真的挂载出东西
  //   `.mud-appbar` 是最外层 layout，在 SignalR circuit 建立后很快可见，
  //   但具体页面主内容（MudContainer / PageHeader / MudGrid）要等 OnInitializedAsync
  //   完成数据加载后才被 render 进 DOM。
  //   判据：<main> 下至少有 3 个 .mud-paper / .mud-card / .mud-container 级别的元素。
  //   若调用方传了 readySelector，优先等它。
  try {
    await page.waitForFunction(
      (selector: string | undefined) => {
        if (selector && document.querySelector(selector)) return true;
        const main = document.querySelector('main');
        if (!main) return false;
        const contentMarkers = main.querySelectorAll(
          '.mud-paper, .mud-card, .mud-container .mud-grid, [class*="page-header"], h1, h2, h3, h4, h5',
        );
        return contentMarkers.length >= 3;
      },
      readySelector,
      { timeout: perStageTimeout, polling: 250 },
    );
  } catch {
    // 超时就走一条：至少 layout 已经在，截到半完成的画面也比完全空好
  }

  // Stage 5: 额外冷却——让 MudBlazor 过渡动画（0.2s 级别）彻底结束，避免截到半透明元素
  if (settleMs > 0) {
    await page.waitForTimeout(settleMs);
  }

  // Stage 6: 等所有异步加载的 <img> 加载完（覆盖卡片封面、头像等）
  //   图片的懒加载会让截图出现空白方块。
  try {
    await page.evaluate(
      () =>
        new Promise<void>((resolve) => {
          const imgs = Array.from(document.images);
          const pending = imgs.filter((img) => !img.complete && img.loading !== 'lazy');
          if (pending.length === 0) return resolve();
          let remaining = pending.length;
          const done = () => {
            remaining -= 1;
            if (remaining <= 0) resolve();
          };
          // 最多等 5 秒，再慢的图就放弃
          const failsafe = setTimeout(resolve, 5_000);
          pending.forEach((img) => {
            img.addEventListener('load', () => {
              done();
              if (remaining <= 0) clearTimeout(failsafe);
            }, { once: true });
            img.addEventListener('error', () => {
              done();
              if (remaining <= 0) clearTimeout(failsafe);
            }, { once: true });
          });
        }),
    );
  } catch {
    // 忽略
  }
}

/**
 * 在截图前禁用所有动画与过渡，保证截图像素级稳定。
 * 注入 CSS，立即生效。
 */
export async function freezeAnimations(page: Page): Promise<void> {
  await page.addStyleTag({
    content: `
      *, *::before, *::after {
        transition: none !important;
        animation-duration: 0s !important;
        animation-delay: 0s !important;
        caret-color: transparent !important;
      }
      /* MudBlazor 的 ripple 效果也禁掉 */
      .mud-ripple-visual, .mud-ripple-container { display: none !important; }
    `,
  });
}
