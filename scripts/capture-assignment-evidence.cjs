const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const path = require('node:path');
(async () => {
  const root = path.resolve(process.argv[2] || 'TestResults/Assignment');
  const browser = await chromium.launch({headless:true});
  try {
    const page = await browser.newPage({viewport:{width:1440,height:1000},deviceScaleFactor:1});
    await page.goto(require('node:url').pathToFileURL(path.join(root,'evidence-index.html')).href);
    await page.screenshot({path:path.join(root,'test-evidence-overview.png')});
    for (const [id,name] of [['unit-results','A1-unit-results.png'],['integration-results','A2-integration-results.png'],['performance-results','A3-load-stress-results.png'],['selenium-results','A4-selenium-results.png']])
      await page.locator('#'+id).screenshot({path:path.join(root,name)});
  } finally { await browser.close(); }
})().catch(error => {console.error(error);process.exitCode=1;});
