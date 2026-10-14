import { Fact, FactSheet } from './contracts';

/** "23:41" in the first half, "45+2" in stoppage, "67:05" in the second half, from period clock alone. */
export function formatClock(period: number, clockMs: number): string {
  if (period <= 0) {
    return '00:00';
  }

  const totalSeconds = Math.floor(clockMs / 1000);
  const half = 45 * 60;
  if (totalSeconds >= half) {
    const over = Math.floor((totalSeconds - half) / 60) + 1;
    return `${period === 1 ? 45 : 90}+${over}`;
  }

  const minutes = Math.floor(totalSeconds / 60) + (period === 2 ? 45 : 0);
  const seconds = totalSeconds % 60;
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}

/** A fact's value as shown: numbers at the fact's precision; text as is. The value is already rounded by the API. */
export function formatFact(fact: Fact | undefined): string {
  if (!fact) {
    return '–';
  }

  return typeof fact.value === 'number' ? fact.value.toFixed(fact.precision) : fact.value;
}

export function findFact(sheet: FactSheet | null | undefined, key: string): Fact | undefined {
  return sheet?.facts.find((f) => f.key === key);
}

export function numberOf(sheet: FactSheet | null | undefined, key: string): number {
  const value = findFact(sheet, key)?.value;
  return typeof value === 'number' ? value : 0;
}

/** A sentence split into plain text and [[fact.key]] tokens, for the control room's citation chips. */
export type SentencePart = { kind: 'text'; text: string } | { kind: 'fact'; key: string };

export function splitTokens(text: string): SentencePart[] {
  const parts: SentencePart[] = [];
  const token = /\[\[([^[\]]+)\]\]/g;
  let last = 0;
  for (let match = token.exec(text); match; match = token.exec(text)) {
    if (match.index > last) {
      parts.push({ kind: 'text', text: text.slice(last, match.index) });
    }

    parts.push({ kind: 'fact', key: match[1].trim() });
    last = match.index + match[0].length;
  }

  if (last < text.length) {
    parts.push({ kind: 'text', text: text.slice(last) });
  }

  return parts;
}

/** Two or three letters for a club monogram: the short name the league gives it. */
export function monogramOf(shortName: string): string {
  return shortName.slice(0, 3).toUpperCase();
}
