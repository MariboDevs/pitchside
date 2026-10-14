import { expect, test } from '@playwright/test';
import { API, MATCH, ensureReplayed } from './fixture';

// Seed 7 has 34 moments, each written for every persona in four languages (WP-06).
const ALL_VARIANTS = 752;

test.beforeAll(async ({ request }) => {
  await ensureReplayed(request);
  await expect
    .poll(
      async () =>
        (
          (await (
            await request.get(`${API}/api/v1/matches/${MATCH}/narratives`)
          ).json()) as unknown[]
        ).length,
      { timeout: 60_000 },
    )
    .toBe(ALL_VARIANTS);
});

test('switching persona and language changes the cards', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/viewer/${MATCH}`);
  const card = page.getByTestId('lower-third').first();
  await expect(card).toBeVisible();

  await page.getByRole('radio', { name: 'Casual' }).click();
  await expect(card).toContainText('give everyone something to talk about');

  await page.getByRole('radio', { name: 'Analyst' }).click();
  await expect(card).toContainText('expected goals read');
  const english = await card.locator('.text').innerText();
  expect(english).toMatch(/\d\.\d/);

  await page.getByRole('radio', { name: 'Afrikaans' }).click();
  await expect(card).toHaveAttribute('lang', 'af');
  await expect(card.locator('.text')).not.toHaveText(english);
  await expect(card.locator('.text')).toHaveText(/\d,\d/);
});

test('a club fan reads the moment through the chosen club', async ({ page }) => {
  await page.goto(`/viewer/${MATCH}`);
  await page.getByRole('radio', { name: 'English' }).click();
  await page.getByRole('radio', { name: 'Club fan' }).click();
  await page.getByLabel('Your club').selectOption('AMZ');
  await expect(page.getByTestId('lower-third').first()).toContainText('Amanzi United supporters');

  await page.getByLabel('Your club').selectOption('INY');
  await expect(page.getByTestId('lower-third').first()).toContainText(
    'Inyoni Wanderers supporters',
  );
});

test('a followed player who is not involved gets the casual card, marked', async ({ page }) => {
  await page.goto(`/viewer/${MATCH}`);
  await page.getByRole('radio', { name: 'English' }).click();
  await page.getByRole('radio', { name: 'Player focus' }).click();
  await page.getByLabel('Player to follow').selectOption('INY-07');

  await expect(page.getByTestId('card-note').first()).toContainText('not involved in this moment');
  await expect(page.getByText('Nathan Kim is right in the middle of it').first()).toBeVisible();
});

test('the control room lists every variant of a moment', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`/control/${MATCH}`);
  await page.getByRole('button', { name: /Goal: Inyoni Wanderers 0-1/ }).click();
  const variants = page.getByTestId('variants').first();
  await expect(variants).toContainText('persona and language variants');
  await variants.locator('summary').click();
  await expect(variants.locator('li')).toHaveCount(27);
  await expect(variants).toContainText('player focus (INY-07) · af');
});
