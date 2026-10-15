import { Moment, Narrative, ViewerPreferences } from './contracts';

/** What a viewer's card shows for a moment: its own variant, a pending slot, or the casual card. */
export type VariantState = 'ready' | 'writing' | 'not_involved';

export interface VariantView {
  narrative: Narrative | null;
  state: VariantState;
  /** The language's translation fell back to the English sentences. */
  englishFallback: boolean;
}

/** The derivable narrative id, `{momentId}:{persona}[.{subject}]:{language}`. */
export function variantId(
  momentId: string,
  persona: string,
  language: string,
  subject: string | null = null,
): string {
  return subject
    ? `${momentId}:${persona}.${subject}:${language}`
    : `${momentId}:${persona}:${language}`;
}

function usable(narrative: Narrative | undefined): boolean {
  return !!narrative && (narrative.status === 'verified' || narrative.status === 'fallback');
}

/**
 * Picks the narrative a viewer reads for a moment (WP06-R2, R3). A variant that has not arrived yet is a "writing for
 * you" slot while the base narrative exists; a rejected variant gives way to the base narrative; a followed player
 * who is not on the moment gets the casual card, marked as not involved.
 */
export function resolveVariant(
  moment: Moment,
  viewer: ViewerPreferences,
  narratives: ReadonlyMap<string, Narrative>,
): VariantView {
  const language = viewer.language;
  const baseCandidate =
    narratives.get(variantId(moment.id, 'base', language)) ??
    narratives.get(variantId(moment.id, 'base', 'en'));
  const base = baseCandidate && usable(baseCandidate) ? baseCandidate : null;

  let target: string | null;
  let state: VariantState = 'ready';
  switch (viewer.persona) {
    case 'analyst':
    case 'casual':
      target = variantId(moment.id, viewer.persona, language);
      break;
    case 'club_fan':
      target = viewer.clubId ? variantId(moment.id, 'club_fan', language, viewer.clubId) : null;
      break;
    case 'player_focus':
      if (viewer.playerId && moment.playerIds.includes(viewer.playerId)) {
        target = variantId(moment.id, 'player_focus', language, viewer.playerId);
      } else {
        target = variantId(moment.id, 'casual', language);
        state = 'not_involved';
      }
      break;
    default:
      target = null;
  }

  if (target === null) {
    return { narrative: base, state, englishFallback: fellBack(base) };
  }

  const variant = narratives.get(target);
  if (variant && usable(variant)) {
    return { narrative: variant, state, englishFallback: fellBack(variant) };
  }

  if (variant?.status === 'rejected' || !base) {
    return { narrative: base, state, englishFallback: fellBack(base) };
  }

  return { narrative: null, state: 'writing', englishFallback: false };
}

/**
 * The language a narrative's text is actually in, for the HTML `lang` attribute: a translation that fell back is
 * English text delivered under its viewer's language, and screen readers must pronounce it as English.
 */
export function textLanguage(narrative: Narrative): string {
  return fellBack(narrative) ? 'en' : narrative.language;
}

function fellBack(narrative: Narrative | null): boolean {
  return !!narrative && narrative.language !== 'en' && narrative.provenance.fallback;
}
