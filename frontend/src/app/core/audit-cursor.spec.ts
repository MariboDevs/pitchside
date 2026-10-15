import { describe, expect, it } from 'vitest';
import { rewindCursor } from './audit-cursor';

describe('rewindCursor', () => {
  /** The 10-character ULID time part of `ms`. */
  const at = (ms: number) => {
    let rest = ms;
    let prefix = '';
    for (let i = 0; i < 10; i++) {
      prefix = '0123456789ABCDEFGHJKMNPQRSTVWXYZ'[rest % 32] + prefix;
      rest = Math.floor(rest / 32);
    }
    return prefix;
  };

  it('moves the time part back and clears the random part', () => {
    const id = at(1792929600000) + '7ZZZZZZZZZZZZZZZ';
    expect(rewindCursor(id, 10_000)).toBe(at(1792929590000) + '0000000000000000');
  });

  it('sorts before every record created inside the window', () => {
    const later = at(1792929595000) + '0000000000000001';
    expect(rewindCursor(at(1792929600000) + 'ABCDEFGHJKMNPQRS', 10_000) < later).toBe(true);
  });

  it('never goes below the epoch', () => {
    expect(rewindCursor(at(5) + '0000000000000000', 10_000)).toBe('0'.repeat(26));
  });
});
