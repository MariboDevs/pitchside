import { expect, test } from '@playwright/test';
import { ensureReplayed, MATCH } from './fixture';
import config from '../ui-invariants.config';

// The ui-invariants layout gate, run inside Playwright: at every configured viewport, no route scrolls sideways.
test.beforeAll(async ({ request }) => {
  await ensureReplayed(request);
});

for (const route of config.routes) {
  for (const viewport of config.viewports) {
    test(`${route.path} has no horizontal overflow at ${viewport.width}px`, async ({ page }) => {
      await page.setViewportSize(viewport);
      await page.goto(route.path.replace(':matchId', MATCH) + (route.query ?? ''));
      await page.waitForSelector(route.ready);
      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - window.innerWidth,
      );
      expect(overflow).toBeLessThanOrEqual(0);
    });
  }
}
