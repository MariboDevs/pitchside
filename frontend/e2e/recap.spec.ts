import { expect, test } from '@playwright/test';
import { API, MATCH, ensureReplayed } from './fixture';

// WP-10: at full time the recap is spoken in each viewer language; the e2e API runs the scripted voice.
test.beforeAll(async ({ request }) => {
  await ensureReplayed(request);
  await expect
    .poll(
      async () => (await request.get(`${API}/api/v1/matches/${MATCH}/recap?language=es`)).status(),
      {
        timeout: 60_000,
      },
    )
    .toBe(200);
});

test('the viewer plays the recap in the chosen language', async ({ page, request }) => {
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto(`/viewer/${MATCH}`);
  await page.getByRole('radio', { name: 'English' }).click();
  const player = page.getByTestId('recap-player');
  await expect(player).toBeVisible();
  await expect(player.locator('audio')).toHaveAttribute('src', /recap\/audio\?language=en$/);

  await page.getByRole('radio', { name: 'isiZulu' }).click();
  await expect(player.locator('audio')).toHaveAttribute('src', /recap\/audio\?language=zu$/);
  const audio = await request.get(`${API}/api/v1/matches/${MATCH}/recap/audio?language=zu`);
  expect(audio.headers()['content-type']).toBe('audio/mpeg');
  expect((await audio.body()).subarray(0, 2)).toEqual(Buffer.from([0xff, 0xfb]));
});

test('the control room shows the SSML of every language at full time', async ({ page }) => {
  await page.goto(`/control/${MATCH}`);
  const moments = page.getByTestId('moments').locator('.moment-head');
  await expect(moments).toHaveCount(34);
  await moments.last().click();
  const ssml = page.getByTestId('recap-ssml');
  await ssml.locator('summary').click();
  await expect(ssml.locator('pre')).toHaveCount(4);
  await expect(ssml.locator('pre').first()).toContainText('<speak ');
});
