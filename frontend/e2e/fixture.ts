import { APIRequestContext, expect } from '@playwright/test';

export const API = 'http://localhost:5180';
export const MATCH = 'MER-7';
export const SCREENS = '../artifacts/screens';

/** A lower third entry as the API returns it. */
export interface Entry {
  id: string;
  momentId: string;
  period: number;
  inAtMs: number;
  state: string;
}

/** Replays seed 7 at speed 0 once and waits until every moment has its narrative on the overlay timeline. */
export async function ensureReplayed(request: APIRequestContext): Promise<Entry[]> {
  const start = await request.post(`${API}/api/v1/replays`, { data: { seed: 7, speed: 0 } });
  expect([202, 409]).toContain(start.status());
  let entries: Entry[] = [];
  await expect
    .poll(
      async () => {
        entries = await (await request.get(`${API}/api/v1/matches/${MATCH}/overlay`)).json();
        return entries.length;
      },
      { timeout: 45_000 },
    )
    .toBe(34);
  return entries;
}
