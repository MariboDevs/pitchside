import { EventType, MatchStatus } from './contracts';

/** How far the overlay's presentation clock may run ahead of the last event, in real time. */
export const PRESENTATION_CAP_MS = 20_000;

/**
 * How far the presentation clock stands past the last event: the real time since it, times the match's time scale
 * (match milliseconds per real millisecond: its replay speed, 1 in real time), so on a faster replay the cards still
 * get their windows between events. While the match plays it stops at PRESENTATION_CAP_MS, so a stalled feed cannot
 * run the overlay ahead of the match; while it rests (isResting) no event is due, so it runs on and every card, a
 * half-time or full-time card queued behind another included, reaches its out time.
 */
export function presentationAhead(elapsedMs: number, timeScale: number, resting = false): number {
  const scale = timeScale > 0 ? timeScale : 1;
  const elapsed = Math.max(0, elapsedMs);
  return Math.round((resting ? elapsed : Math.min(elapsed, PRESENTATION_CAP_MS)) * scale);
}

/**
 * Whether no event is due for a while: the match has finished, or the half-time whistle (the last event) has gone and
 * the break is on. The match stays live through the break, so the last event is what tells the two apart.
 */
export function isResting(
  status: MatchStatus | undefined,
  lastEventType: EventType | undefined,
): boolean {
  return status === 'completed' || status === 'stopped' || lastEventType === 'half_end';
}
