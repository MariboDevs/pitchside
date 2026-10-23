import { PRESENTATION_CAP_MS, isResting, presentationAhead } from './presentation';

describe('presentationAhead', () => {
  it('runs in real time on a real-time match', () => {
    expect(presentationAhead(4_000, 1)).toBe(4_000);
  });

  it('runs at the replay speed on a faster replay', () => {
    expect(presentationAhead(4_000, 3)).toBe(12_000);
  });

  it('stops at the cap, which is real time, scaled with the clock', () => {
    expect(presentationAhead(60_000, 1)).toBe(PRESENTATION_CAP_MS);
    expect(presentationAhead(60_000, 3)).toBe(3 * PRESENTATION_CAP_MS);
  });

  it('runs on past the cap once the match has finished, so a queued full-time card still leaves', () => {
    // Seed 26 at speed 3: the full-time card queues behind the last distance card and ends about 67 s of match time
    // after the last event, beyond the live cap of 60 s, so a capped clock would leave it on screen for good.
    expect(presentationAhead(23_000, 3)).toBe(60_000);
    expect(presentationAhead(23_000, 3, true)).toBe(69_000);
  });

  it('runs on through the half-time break, so a queued half-time card leaves before the restart', () => {
    // At 3x a half-time card queued behind a late red card runs from +28.85 s to +61.85 s of match time after the
    // whistle; a capped clock stops at +60 s and would keep it on screen for the whole break.
    expect(presentationAhead(21_000, 3)).toBeLessThan(61_850);
    expect(presentationAhead(21_000, 3, true)).toBeGreaterThan(61_850);
  });

  it('treats a match as resting once it has finished or the half-time whistle has gone', () => {
    expect(isResting('completed', 'full_time')).toBe(true);
    expect(isResting('stopped', 'pass')).toBe(true);
    expect(isResting('live', 'half_end')).toBe(true);
    expect(isResting('live', 'pass')).toBe(false);
    expect(isResting('paused', 'shot')).toBe(false);
    expect(isResting(undefined, undefined)).toBe(false);
  });

  it('treats a missing or zero scale as real time and never runs backwards', () => {
    expect(presentationAhead(4_000, 0)).toBe(4_000);
    expect(presentationAhead(-500, 3)).toBe(0);
  });
});
