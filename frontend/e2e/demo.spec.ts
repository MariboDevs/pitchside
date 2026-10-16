import { expect, test } from '@playwright/test';
import { API } from './fixture';

// WP-11: a match being played can be jumped forward to ten seconds before its next moment. A match id replays once
// per store and a local run may reuse an API, so each run plays a seed of its own.
const SEED = 100_000 + (Date.now() % 900_000);
const LIVE = `MER-${SEED}`;

test.afterAll(async ({ request }) => {
  await request.delete(`${API}/api/v1/replays/${LIVE}`);
});

test('the viewer jumps a live match forward to its next moment', async ({ page, request }) => {
  const start = await request.post(`${API}/api/v1/replays`, { data: { seed: SEED, speed: 1 } });
  expect(start.status()).toBe(202);

  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto(`/viewer/${LIVE}`);
  const jump = page.getByTestId('jump');
  await expect(jump).toBeVisible({ timeout: 15_000 });
  await jump.click();
  await expect(page.getByRole('status').filter({ hasText: 'Next up:' })).toBeVisible();
});
