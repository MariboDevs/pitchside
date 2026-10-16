import { expect, test } from '@playwright/test';
import { API } from './fixture';

// WP-11: a match being played can be jumped forward to ten seconds before its next moment. A match id replays once
// per store and a local run may reuse an API, so each run plays a seed of its own.
const SEED = 100_000 + (Date.now() % 900_000);
const LIVE = `MER-${SEED}`;
const CONTROL_SEED = SEED + 1;
const CONTROL_LIVE = `MER-${CONTROL_SEED}`;

test.afterAll(async ({ request }) => {
  await request.delete(`${API}/api/v1/replays/${LIVE}`);
  await request.delete(`${API}/api/v1/replays/${CONTROL_LIVE}`);
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

test('Space on the control room jump button jumps rather than pausing', async ({
  page,
  request,
}) => {
  const start = await request.post(`${API}/api/v1/replays`, {
    data: { seed: CONTROL_SEED, speed: 1 },
  });
  expect(start.status()).toBe(202);

  await page.goto(`/control/${CONTROL_LIVE}`);
  await expect(page.getByRole('button', { name: 'Pause' })).toBeVisible({ timeout: 15_000 });
  const jumped = page.waitForRequest(
    (r) => r.method() === 'POST' && r.url().endsWith(`/replays/${CONTROL_LIVE}/jump`),
  );
  await page.getByTestId('jump').focus();
  await page.keyboard.press('Space');
  await jumped;
  await expect(page.getByRole('button', { name: 'Pause' })).toBeVisible();
});
