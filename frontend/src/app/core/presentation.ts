/** How far the overlay's presentation clock may run ahead of the last event, in real time. */
export const PRESENTATION_CAP_MS = 20_000;

/**
 * How far the presentation clock stands past the last event: the real time since it, at most PRESENTATION_CAP_MS,
 * times the match's time scale (match milliseconds per real millisecond: its replay speed, 1 in real time), so on a
 * faster replay the cards still get their windows between events.
 */
export function presentationAhead(elapsedMs: number, timeScale: number): number {
  const scale = timeScale > 0 ? timeScale : 1;
  return Math.round(Math.min(Math.max(0, elapsedMs), PRESENTATION_CAP_MS) * scale);
}
