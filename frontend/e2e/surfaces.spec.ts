import { expect, test } from '@playwright/test';
import { ensureReplayed, Entry, MATCH, SCREENS } from './fixture';

let goal: Entry;

test.beforeAll(async ({ request }) => {
  const entries = await ensureReplayed(request);
  goal = entries.find((e) => e.momentId.endsWith(':goal') && e.state !== 'expired')!;
});

test('home lists the match and links to its surfaces', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto('/');
  await expect(page.getByTestId('match-row').first()).toBeVisible();
  // The list shows the newest finished matches first; other specs replay more, so open the full list when offered.
  const showAll = page.getByTestId('show-all');
  if ((await showAll.count()) > 0 && (await showAll.getAttribute('aria-expanded')) === 'false') {
    await showAll.click();
  }
  // Other specs replay more matches, some with the same clubs, so the sample's row is found by its own link.
  const row = page
    .getByTestId('match-row')
    .filter({ has: page.locator(`a[href="/control/${MATCH}"]`) });
  await expect(row).toHaveCount(1);
  await expect(row).toContainText('Amanzi United v Inyoni Wanderers');
  await expect(row.getByRole('link', { name: 'Control room' })).toBeVisible();
  await page.screenshot({ path: `${SCREENS}/home.png`, fullPage: true });
});

test('the 16:9 overlay draws the score bug and the lower third on air', async ({ page }) => {
  await page.setViewportSize({ width: 1920, height: 1080 });
  await page.goto(`/overlay/${MATCH}?aspect=16x9&at=${goal.period}:${goal.inAtMs + 1000}`);
  await expect(page.getByTestId('score-bug')).toBeVisible();
  const card = page.getByTestId('lower-third');
  await expect(card).toBeVisible();
  await expect(card).toContainText('Goal:');
  await expect(page.locator('[data-testid="lower-third"]')).toHaveCount(1);

  const background = await page.evaluate(
    () => getComputedStyle(document.documentElement).backgroundColor,
  );
  expect(background).toBe('rgba(0, 0, 0, 0)');

  // Title-safe: nothing within 5% of an edge.
  // Measure the card at rest: its enter animation slides it up 24px, and the animation is not always
  // visible to getAnimations() when the card first appears, so poll its edge until it settles.
  const box = (await card.boundingBox())!;
  expect(box.x).toBeGreaterThanOrEqual(1920 * 0.05 - 1);
  await expect
    .poll(async () => {
      const b = (await card.boundingBox())!;
      return b.y + b.height;
    })
    .toBeLessThanOrEqual(1080 * 0.95 + 1);

  // Broadcast minimum: no text under 28 px at 1080p, anywhere on the stage.
  const sizes = await page.evaluate(() =>
    [...document.querySelectorAll('.stage h2, .stage p, .stage span, .stage text')]
      .filter((node) => (node.textContent ?? '').trim().length > 0)
      .map((node) => parseFloat(getComputedStyle(node).fontSize)),
  );
  expect(Math.min(...sizes)).toBeGreaterThanOrEqual(28);
  await page.screenshot({ path: `${SCREENS}/overlay-16x9.png` });
});

test('the 9:16 overlay centres the score bug and runs the xG race down the right edge', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1080, height: 1920 });
  await page.goto(`/overlay/${MATCH}?aspect=9x16&at=${goal.period}:${goal.inAtMs + 1000}`);
  const bug = (await page.getByTestId('score-bug').boundingBox())!;
  expect(Math.abs(bug.x + bug.width / 2 - 540)).toBeLessThan(20);
  const race = (await page.getByTestId('xg-race').boundingBox())!;
  expect(race.height).toBeGreaterThan(race.width);
  expect(race.x).toBeGreaterThan(540);
  await expect(page.getByTestId('lower-third')).toBeVisible();
  await page.screenshot({ path: `${SCREENS}/overlay-9x16.png` });
});

test('the overlay preview switches aspect, copies a clean OBS URL and keeps clear of the graphics', async ({
  page,
  context,
}) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  const at = `${goal.period}:${goal.inAtMs + 1000}`;
  await page.setViewportSize({ width: 1920, height: 1080 });
  await page.goto(`/overlay/${MATCH}?aspect=16x9&preview=1&lang=af&at=${at}`);
  const toolbar = page.getByTestId('overlay-preview');
  await expect(toolbar).toBeVisible();
  await expect(page.getByTestId('site-header')).toHaveCount(0);
  await expect(page.getByTestId('score-bug')).toBeVisible();

  // Preview only: a studio ground in place of the transparent broadcast page.
  const background = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
  expect(background).not.toBe('rgba(0, 0, 0, 0)');

  await toolbar.getByRole('link', { name: '9:16' }).click();
  await expect(page).toHaveURL(/aspect=9x16/);
  await expect(page).toHaveURL(/preview=1/);
  const race = (await page.getByTestId('xg-race').boundingBox())!;
  expect(race.height).toBeGreaterThan(race.width);

  // The copied URL keeps the rendering options and drops the preview chrome.
  await toolbar.getByRole('button', { name: 'Copy OBS URL' }).click();
  await expect(toolbar.getByRole('button', { name: 'Copied' })).toBeVisible();
  const copied = new URL(await page.evaluate(() => navigator.clipboard.readText()));
  expect(copied.pathname).toBe(`/overlay/${MATCH}`);
  expect(copied.searchParams.get('aspect')).toBe('9x16');
  expect(copied.searchParams.get('lang')).toBe('af');
  expect(copied.searchParams.get('at')).toBe(at);
  expect(copied.searchParams.has('preview')).toBe(false);

  // On a phone the toolbar is a strip above the stage: no overlap with the score bug, no sideways scroll.
  await page.setViewportSize({ width: 360, height: 780 });
  await page.goto(`/overlay/${MATCH}?aspect=16x9&preview=1&at=${at}`);
  const strip = (await toolbar.boundingBox())!;
  const bug = (await page.getByTestId('score-bug').boundingBox())!;
  expect(strip.y).toBe(0);
  expect(bug.y).toBeGreaterThanOrEqual(strip.y + strip.height);
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - window.innerWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
  await page.screenshot({ path: `${SCREENS}/overlay-preview-phone.png` });
});

test('the viewer switches persona and follows a player', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/viewer/${MATCH}`);
  await expect(page.getByTestId('score-bug')).toBeVisible();
  await expect(page.getByTestId('pitch')).toBeVisible();
  await expect(page.getByTestId('lower-third').first()).toBeVisible();

  const analyst = page.getByRole('radio', { name: 'Analyst' });
  await analyst.click();
  await expect(analyst).toHaveAttribute('aria-checked', 'true');

  await page.getByRole('radio', { name: 'Player focus' }).click();
  await page.getByLabel('Player to follow').selectOption('INY-07');
  await expect(page.getByTestId('player-strip')).toContainText('Nathan Kim');
  await page.screenshot({ path: `${SCREENS}/viewer-phone.png`, fullPage: true });

  await page.setViewportSize({ width: 1280, height: 900 });
  await page.screenshot({ path: `${SCREENS}/viewer-desktop.png`, fullPage: true });
});

test('the control room shows the feed, the moments and the citation chain', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`/control/${MATCH}`);
  await expect(page.getByTestId('feed').locator('li').first()).toBeVisible();
  const moments = page.getByTestId('moments').locator(':scope > li');
  await expect(moments).toHaveCount(34);

  await page.getByRole('button', { name: /Goal: Inyoni Wanderers 0-1/ }).click();
  const fact = page.locator('.moment-body .fact').first();
  await expect(fact).not.toHaveText('…');
  await fact.hover();
  await expect(page.getByText('Verdict')).toBeVisible();
  // WP-07: each stage's model calls from the audit, and the usage guard's counts.
  await expect(page.getByTestId('stage-timing').first()).toContainText('call');
  await expect(page.getByTestId('usage')).toContainText('Moments narrated this hour');
  await page.screenshot({ path: `${SCREENS}/control.png`, fullPage: true });

  await page.keyboard.press('j');
  await expect(page.locator('.moment--selected')).toHaveCount(1);
});
