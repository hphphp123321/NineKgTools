// 诊断脚本 v2：拦截 blazor.server.js，看 SSR-only 状态下主内容的位置
import { chromium } from '@playwright/test';
import fs from 'node:fs/promises';

const BASE = 'http://127.0.0.1:23333';
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({
  storageState: await fs.access('./auth.json').then(() => './auth.json').catch(() => undefined),
  viewport: { width: 1440, height: 900 },
  deviceScaleFactor: 2,
});
const page = await context.newPage();

// 拦截 Blazor 相关 JS（和 capture.spec 一样）
await page.route('**/_framework/blazor.server.js*', (route) => route.fulfill({ status: 200, body: '/* blocked */' }));
await page.route('**/_content/MudBlazor/MudBlazor.min.js*', (route) => route.fulfill({ status: 200, body: '/* blocked */' }));
await page.route('**/js/photo-wall.js*', (route) => route.fulfill({ status: 200, body: '/* blocked */' }));
await page.route('**/js/search.js*', (route) => route.fulfill({ status: 200, body: '/* blocked */' }));
await page.route('**/js/settings.js*', (route) => route.fulfill({ status: 200, body: '/* blocked */' }));

const failedRequests = [];
page.on('response', (r) => {
  if (r.status() >= 400) failedRequests.push(`${r.status()} ${r.url()}`);
});

console.log(`\n=== goto ${BASE}/ ===`);
await page.goto(`${BASE}/`, { waitUntil: 'load' });
await page.waitForTimeout(2000);

const diag = await page.evaluate(() => {
  const pickRect = (sel) => {
    const el = document.querySelector(sel);
    if (!el) return null;
    const r = el.getBoundingClientRect();
    return { x: r.x, y: r.y, w: r.width, h: r.height, classes: el.className };
  };
  const mudPapers = Array.from(document.querySelectorAll('.mud-paper'));
  return {
    bodyScroll: { w: document.body.scrollWidth, h: document.body.scrollHeight },
    viewport: { w: window.innerWidth, h: window.innerHeight },
    main: pickRect('main') ?? pickRect('.mud-main-content'),
    firstMudContainer: pickRect('.mud-container'),
    firstMudPaper: mudPapers.length > 0 ? {
      count: mudPapers.length,
      rect: mudPapers[0].getBoundingClientRect().toJSON?.() ?? { x: mudPapers[0].getBoundingClientRect().x, y: mudPapers[0].getBoundingClientRect().y },
    } : null,
    has媒体总览: document.body.innerText.includes('媒体总览'),
    bodyText首200: document.body.innerText.substring(0, 200),
    photoWallContainer: pickRect('.photo-wall-container, .photo-wall-wrapper, [class*="photo-wall"]'),
    appBar: pickRect('.mud-appbar'),
  };
});

console.log('\n=== 诊断数据 ===');
console.log(JSON.stringify(diag, null, 2));

console.log('\n=== 失败请求（4xx/5xx）===');
if (failedRequests.length === 0) console.log('（无）');
else failedRequests.forEach((r) => console.log(r));

console.log('\n=== 截 2 张图：viewport 和 fullPage ===');
await page.screenshot({ path: './diagnose-viewport.png', fullPage: false });
await page.screenshot({ path: './diagnose-fullpage.png', fullPage: true });
const vp = await fs.stat('./diagnose-viewport.png');
const fp = await fs.stat('./diagnose-fullpage.png');
console.log(`diagnose-viewport.png: ${vp.size} bytes`);
console.log(`diagnose-fullpage.png: ${fp.size} bytes`);

await browser.close();
console.log('\n诊断完成');
