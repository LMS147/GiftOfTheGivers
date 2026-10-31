// Screenshot actual server-rendered smoke-test pages using local assets.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const fs = require('node:fs/promises');
const path = require('node:path');
const http = require('node:http');
(async () => {
  const snapshots = path.resolve('ui-snapshots');
  const assets = path.resolve('wwwroot');
  const server = http.createServer(async (req, res) => {
    const pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
    const root = pathname.endsWith('.html') ? snapshots : assets;
    const file = path.resolve(root, '.' + pathname);
    if (!file.startsWith(root + path.sep)) { res.writeHead(403).end(); return; }
    try {
      const mime = { '.html':'text/html', '.css':'text/css', '.js':'text/javascript', '.svg':'image/svg+xml', '.woff2':'font/woff2' }[path.extname(file)] || 'application/octet-stream';
      res.writeHead(200, {'Content-Type':mime}); res.end(await fs.readFile(file));
    } catch { res.writeHead(404).end(); }
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const port = server.address().port;
  const browser = await chromium.launch({headless:true});
  const failures = [];
  try {
    const pages = (await fs.readdir(snapshots)).filter(p => p.endsWith('.html'));
    if (!pages.length) throw new Error('No rendered pages available.');
    for (const width of [1440, 390]) {
      const page = await browser.newPage({viewport:{width,height:1000}, reducedMotion:'reduce'});
      for (const file of pages) {
        await page.goto(`http://127.0.0.1:${port}/${file}`, {waitUntil:'networkidle'});
        await page.evaluate(() => document.fonts.ready);
        const overflow = await page.evaluate(() => ({width:innerWidth, content:document.documentElement.scrollWidth}));
        if (overflow.content > overflow.width + 1) failures.push(`${file} overflows at ${width}px: ${overflow.content}px`);
        const broken = await page.locator('img').evaluateAll(images => images.filter(i => !i.complete || i.naturalWidth === 0).map(i => i.src));
        if (broken.length) failures.push(`${file}: broken images: ${broken.join(',')}`);
        await page.screenshot({path:path.join(snapshots, file.replace('.html', `-${width}.png`)),fullPage:true});
      }
      await page.close();
    }
    await fs.writeFile(path.join(snapshots,'visual-checks.json'), JSON.stringify({failures},null,2));
    if (failures.length) throw new Error(failures.join('\n'));
    console.log(`${pages.length} pages checked at desktop and mobile widths; no horizontal overflow or broken images.`);
  } finally { await browser.close(); server.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
