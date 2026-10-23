import { PRESENTATION_CAP_MS, presentationAhead } from './presentation';

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

  it('treats a missing or zero scale as real time and never runs backwards', () => {
    expect(presentationAhead(4_000, 0)).toBe(4_000);
    expect(presentationAhead(-500, 3)).toBe(0);
  });
});
