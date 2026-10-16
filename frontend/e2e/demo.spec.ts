import { expect, test } from '@playwright/test';
import { API } from './fixture';

// WP-11: a match being played can be jumped forward to ten seconds before its next moment.
const LIVE = 'MER-110';

test.afterAll(async ({ request }) => {
  await request.delete(`${API}/api/v1/replays/${LIVE}`);
});

test('the viewer jumps a live match forward to its next moment', async ({ page, request }) => {
  const start = await request.post(`${API}/api/v1/replays`, { data: { seed: 110, speed: 1 } });
  expect([202, 409]).toContain(start.status());

  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto(`/viewer/${LIVE}`);
  const jump = page.getByTestId('jump');
  await expect(jump).toBeVisible({ timeout: 15_000 });
  await jump.click();
  await expect(page.getByRole('status').filter({ hasText: 'Next up:' })).toBeVisible();
});
