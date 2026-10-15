import { describe, expect, it } from 'vitest';
import { formatClock, formatFact, splitTokens } from './format';

describe('formatClock', () => {
  it('shows minutes and seconds in the first half', () =>
    expect(formatClock(1, 23 * 60_000 + 41_000)).toBe('23:41'));
  it('adds 45 in the second half', () => expect(formatClock(2, 22 * 60_000 + 5_000)).toBe('67:05'));
  it('shows stoppage as plus minutes', () => {
    expect(formatClock(1, 46 * 60_000 + 10_000)).toBe('45+2');
    expect(formatClock(2, 45 * 60_000)).toBe('90+1');
  });
  it('reads zero before kick-off', () => expect(formatClock(0, 0)).toBe('00:00'));
});

describe('formatFact', () => {
  it('uses the precision', () =>
    expect(formatFact({ key: 'home.xg', value: 1.5, precision: 2, derivedFrom: [] })).toBe('1.50'));
  it('passes text through', () =>
    expect(
      formatFact({ key: 'match.state', value: 'full_time', precision: 0, derivedFrom: [] }),
    ).toBe('full_time'));
  it('shows a dash for a missing fact', () => expect(formatFact(undefined)).toBe('–'));
});

describe('splitTokens', () => {
  it('splits text and fact tokens in order', () =>
    expect(splitTokens('Lead [[match.score.home]]-[[match.score.away]] now')).toEqual([
      { kind: 'text', text: 'Lead ' },
      { kind: 'fact', key: 'match.score.home' },
      { kind: 'text', text: '-' },
      { kind: 'fact', key: 'match.score.away' },
      { kind: 'text', text: ' now' },
    ]));
});
