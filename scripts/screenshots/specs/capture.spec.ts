/**
 * NineKgTools README 截图任务
 *
 * 6 张截图，对应 README 里的"项目速览"小节：
 *   home.png      — /
 *   overview.png  — /media/overview
 *   search.png    — /search
 *   pending.png   — /source/pending
 *   tasks.png     — /tasks
 *   settings.png  — /settings
 *
 * 输出到 docs/assets/screenshots/（相对仓库根）
 *
 * **策略**（经诊断后确定）：
 *   Blazor Server 的客户端 hydration 会在 MudBlazor JS interop 失败时
 *   终止整个 circuit 并清空部分 DOM（诊断脚本记录：从 1158 字符降到 569 字符）。
 *   截图任务只要**静态呈现**，不需要页面可交互。
 *   因此最稳妥的做法是**阻止 blazor.server.js 加载**，完全依赖 Blazor Server 的
 *   prerender 产出的完整 SSR HTML。
 *   副作用：按钮不可点、动画不运行，但这些对截图无害且反而更稳定。
 */
import { test, expect } from '@playwright/test';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { freezeAnimations, waitForBlazor } from '../utils/wait-for-blazor.ts';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUTPUT_DIR = path.resolve(__dirname, '../../../docs/assets/screenshots');

test.beforeEach(async ({ page }) => {
  // 阻止 Blazor 客户端脚本加载——只保留 SSR 完整渲染
  await page.route('**/_framework/blazor.server.js*', (route) =>
    route.fulfill({ status: 200, contentType: 'application/javascript', body: '/* screenshot mode: blazor disabled */' }),
  );
  // 同时禁掉 MudBlazor JS（它在没有 circuit 时会抛错误）
  await page.route('**/_content/MudBlazor/MudBlazor.min.js*', (route) =>
    route.fulfill({ status: 200, contentType: 'application/javascript', body: '/* screenshot mode: mud js disabled */' }),
  );
  // 项目自有的 JS interop 脚本也禁掉
  await page.route('**/js/photo-wall.js*', (route) =>
    route.fulfill({ status: 200, contentType: 'application/javascript', body: '/* screenshot mode */' }),
  );
  await page.route('**/js/search.js*', (route) =>
    route.fulfill({ status: 200, contentType: 'application/javascript', body: '/* screenshot mode */' }),
  );
  await page.route('**/js/settings.js*', (route) =>
    route.fulfill({ status: 200, contentType: 'application/javascript', body: '/* screenshot mode */' }),
  );
});

interface Shot {
  name: string;
  route: string;
  /** 业务就绪的标志元素（非必填，不给则使用默认 .mud-layout） */
  readySelector?: string;
  /** 截图是否包含整页（true）还是只截视口（false，默认） */
  fullPage?: boolean;
  /** 额外等待，适合数据加载慢的页面 */
  extraWaitMs?: number;
}

const shots: Shot[] = [
  // 核心展示
  { name: 'home',         route: '/',                                           extraWaitMs: 500 },
  { name: 'overview',     route: '/media/overview',                             extraWaitMs: 500 },
  { name: 'search',       route: '/search?q=' + encodeURIComponent('示例'),     extraWaitMs: 500 },
  { name: 'pending',      route: '/source/pending',                             extraWaitMs: 500 },
  { name: 'tasks',        route: '/tasks',                                      extraWaitMs: 500 },
  { name: 'settings',     route: '/settings',                                   extraWaitMs: 500 },
  // 扩充：管理与配置
  { name: 'website',      route: '/website',                                    extraWaitMs: 500 },
  { name: 'tag-mappings', route: '/tags/mappings',                              extraWaitMs: 500 },
  { name: 'creators',     route: '/creators',                                   extraWaitMs: 500 },
  { name: 'favorites',    route: '/favorites',                                  extraWaitMs: 500 },
  // 已剔除的截图：
  //   tags.png    —— TagsPage 异步加载，SSR 阶段只显示骨架屏
  //   sources.png —— 文件浏览器暴露用户机器目录树（C:\Users\xxx）
];

for (const shot of shots) {
  test(`capture: ${shot.name}`, async ({ page }) => {
    await page.goto(shot.route, { waitUntil: 'domcontentloaded' });

    // SSR 模式下 main 内容直接在 HTML 里，等它出现即可
    await page.locator('main, .mud-main-content').first().waitFor({ state: 'visible', timeout: 10_000 });

    // 等图片加载（SSR 下所有 <img> 都是真实 src）
    await page.evaluate(
      () =>
        new Promise<void>((resolve) => {
          const imgs = Array.from(document.images).filter((img) => !img.complete);
          if (imgs.length === 0) return resolve();
          let remaining = imgs.length;
          const tick = () => {
            remaining -= 1;
            if (remaining <= 0) resolve();
          };
          const failsafe = setTimeout(resolve, 4_000);
          imgs.forEach((img) => {
            img.addEventListener('load', tick, { once: true });
            img.addEventListener('error', tick, { once: true });
          });
          void failsafe;
        }),
    );

    await freezeAnimations(page);
    await page.waitForTimeout(shot.extraWaitMs ?? 500);

    // 滚动到顶
    await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'instant' as ScrollBehavior }));
    await page.waitForTimeout(150);

    const outputPath = path.join(OUTPUT_DIR, `${shot.name}.png`);
    await page.screenshot({
      path: outputPath,
      fullPage: shot.fullPage ?? false,
      animations: 'disabled',
      caret: 'hide',
      scale: 'device',
    });

    expect(outputPath).toBeTruthy();
  });
}
