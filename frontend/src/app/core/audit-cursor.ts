const ALPHABET = '0123456789ABCDEFGHJKMNPQRSTVWXYZ';

/** How far back each audit read starts, so a record stored after a later one (calls run in parallel) is not skipped. */
export const AUDIT_OVERLAP_MS = 10_000;

/**
 * The audit cursor moved back by `ms`: the 10-character time part of a ULID less `ms`, then the lowest random part, so
 * the next read also returns every record created in that window. Records already held are dropped by id.
 */
export function rewindCursor(id: string, ms = AUDIT_OVERLAP_MS): string {
  let time = 0;
  for (const c of id.slice(0, 10)) {
    time = time * 32 + ALPHABET.indexOf(c);
  }

  let rest = Math.max(0, time - ms);
  let prefix = '';
  for (let i = 0; i < 10; i++) {
    prefix = ALPHABET[rest % 32] + prefix;
    rest = Math.floor(rest / 32);
  }

  return prefix + '0'.repeat(16);
}
