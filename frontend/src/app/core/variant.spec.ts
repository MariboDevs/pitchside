import { describe, expect, it } from 'vitest';
import { Moment, Narrative } from './contracts';
import { VARIANT_WAIT_MS, resolveVariant, textLanguage, variantId } from './variant';

const moment = { id: 'MER-7:M:0324:goal', playerIds: ['INY-07', 'INY-04'] } as Moment;

function narrative(
  id: string,
  status: Narrative['status'] = 'verified',
  fallback = false,
): Narrative {
  const [, , , , personaPart, language] = id.split(':');
  const [persona, subject] = personaPart.split('.');
  return {
    id,
    persona,
    subject: subject ?? null,
    language,
    status,
    renderedText: id,
    provenance: { fallback },
  } as Narrative;
}

function map(...items: Narrative[]): ReadonlyMap<string, Narrative> {
  return new Map(items.map((n) => [n.id, n]));
}

describe('resolveVariant', () => {
  const base = narrative(variantId(moment.id, 'base', 'en'));

  it('shows the viewer persona in its language', () => {
    const analyst = narrative(variantId(moment.id, 'analyst', 'af'));
    const view = resolveVariant(moment, { persona: 'analyst', language: 'af' }, map(base, analyst));
    expect(view.narrative).toBe(analyst);
    expect(view.state).toBe('ready');
  });

  it('holds a writing slot until the variant arrives', () => {
    const view = resolveVariant(moment, { persona: 'casual', language: 'en' }, map(base));
    expect(view).toEqual({ narrative: null, state: 'writing', englishFallback: false });
  });

  it('falls back to the base narrative when the variant was rejected', () => {
    const rejected = narrative(variantId(moment.id, 'casual', 'en'), 'rejected');
    expect(
      resolveVariant(moment, { persona: 'casual', language: 'en' }, map(base, rejected)).narrative,
    ).toBe(base);
  });

  it('picks the club fan variant for the followed club', () => {
    const amz = narrative(variantId(moment.id, 'club_fan', 'en', 'AMZ'));
    const iny = narrative(variantId(moment.id, 'club_fan', 'en', 'INY'));
    const view = resolveVariant(
      moment,
      { persona: 'club_fan', language: 'en', clubId: 'INY' },
      map(base, amz, iny),
    );
    expect(view.narrative).toBe(iny);
  });

  it('shows the casual card, marked, when the followed player is not involved', () => {
    const casual = narrative(variantId(moment.id, 'casual', 'en'));
    const view = resolveVariant(
      moment,
      { persona: 'player_focus', language: 'en', playerId: 'AMZ-09' },
      map(base, casual),
    );
    expect(view.narrative).toBe(casual);
    expect(view.state).toBe('not_involved');
  });

  it('flags a translation that fell back to English', () => {
    const spanish = narrative(variantId(moment.id, 'analyst', 'es'), 'fallback', true);
    expect(
      resolveVariant(moment, { persona: 'analyst', language: 'es' }, map(base, spanish))
        .englishFallback,
    ).toBe(true);
  });

  it('falls back to the base when the variant was rejected in the viewer language', () => {
    const rejected = narrative(variantId(moment.id, 'casual', 'zu'), 'rejected');
    const view = resolveVariant(moment, { persona: 'casual', language: 'zu' }, map(base, rejected));
    expect(view.narrative).toBe(base);
    expect(view.state).toBe('ready');
  });
});

describe('textLanguage', () => {
  it('marks a fallen-back translation as English and keeps a real one', () => {
    expect(textLanguage(narrative(variantId(moment.id, 'analyst', 'af'), 'fallback', true))).toBe(
      'en',
    );
    expect(textLanguage(narrative(variantId(moment.id, 'analyst', 'af')))).toBe('af');
  });
});

describe('waiting for a variant', () => {
  const generated = Date.parse('2026-10-25T15:00:00Z');
  const base = {
    ...narrative(variantId(moment.id, 'base', 'en')),
    provenance: { fallback: false, generatedUtc: '2026-10-25T15:00:00Z' },
  } as Narrative;

  it('holds the writing slot while the variant may still come', () => {
    const view = resolveVariant(
      moment,
      { persona: 'casual', language: 'zu' },
      map(base),
      generated + 5_000,
    );
    expect(view.state).toBe('writing');
  });

  it('shows the base once the variant is overdue', () => {
    const view = resolveVariant(
      moment,
      { persona: 'casual', language: 'zu' },
      map(base),
      generated + VARIANT_WAIT_MS + 1,
    );
    expect(view.narrative).toBe(base);
    expect(view.state).toBe('ready');
  });
});
