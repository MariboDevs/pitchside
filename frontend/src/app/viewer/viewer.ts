import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { Club, LANGUAGES, PERSONAS, Player, RECAP_POLL_MS, Recap } from '../core/contracts';
import { toApiError } from '../core/api-error';
import { MatchApi } from '../core/match-api';
import { findFact, formatFact } from '../core/format';
import { MatchSession } from '../core/match-session';
import { resolveVariant, textLanguage } from '../core/variant';
import { ViewerStore } from '../core/viewer-store';
import { LowerThird, LowerThirdChip } from '../shared/lower-third/lower-third';
import { Pitch } from '../shared/pitch/pitch';
import { RecapPlayer } from '../shared/recap-player/recap-player';
import { ScoreBug } from '../shared/score-bug/score-bug';
import { SegmentedControl } from '../shared/segmented-control/segmented-control';

/** A card in the viewer's stack. */
interface ViewerCard {
  id: string;
  club: Club | null;
  headline: string;
  text: string;
  lang: string;
  chips: LowerThirdChip[];
  pending: boolean;
  note: string | null;
}

const PLAYER_STRIP = [
  { metric: 'minutes', label: 'Minutes' },
  { metric: 'distanceKm', label: 'Distance km' },
  { metric: 'topSpeedKph', label: 'Top speed' },
  { metric: 'sprints', label: 'Sprints' },
  { metric: 'passes', label: 'Passes' },
  { metric: 'passCompletion', label: 'Completion' },
  { metric: 'xg', label: 'xG' },
];

/**
 * The fan's second screen: score and clock, the pitch with the latest events, live cards in the chosen persona
 * and language, and a strip of the followed player's numbers in player focus.
 */
@Component({
  selector: 'app-viewer',
  imports: [LowerThird, Pitch, RecapPlayer, ScoreBug, SegmentedControl],
  templateUrl: './viewer.html',
  styleUrl: './viewer.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [MatchSession],
})
export class Viewer implements OnInit {
  protected readonly session = inject(MatchSession);
  private readonly store = inject(ViewerStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly api = inject(MatchApi);

  readonly matchId = input.required<string>();

  protected readonly personas = PERSONAS;
  protected readonly languages = LANGUAGES;
  protected readonly preferences = this.store.preferences;

  /** The spoken full-time recap in the chosen language, once the match has reached full time (WP-10). */
  protected readonly recap = signal<{ recap: Recap; src: string } | null>(null);
  protected readonly recapError = signal<string | null>(null);

  /**
   * The chosen language alone, so a change of persona, club or player (which replaces the whole preferences object)
   * does not reload the recap and cut off what is playing.
   */
  private readonly language = computed(() => this.preferences().language);

  private readonly fullTime = computed(() =>
    this.session.moments().some((m) => m.type === 'full_time'),
  );

  /** The wall clock, a second at a time, so a variant that never arrives gives way to the base narrative. */
  private readonly now = signal(Date.now());

  protected readonly squads = computed(() => {
    const doc = this.session.document();
    return doc
      ? [
          { club: doc.home.club, players: [...doc.home.starters, ...doc.home.bench] },
          { club: doc.away.club, players: [...doc.away.starters, ...doc.away.bench] },
        ]
      : [];
  });

  protected readonly followed = computed<Player | null>(() => {
    const id = this.preferences().playerId;
    return (
      this.squads()
        .flatMap((s) => s.players)
        .find((p) => p.id === id) ?? null
    );
  });

  protected readonly cards = computed<ViewerCard[]>(() => {
    const doc = this.session.document();
    if (!doc) {
      return [];
    }

    const viewer = this.preferences();
    const narratives = this.session.narratives();
    const cards: ViewerCard[] = [];
    for (const moment of [...this.session.moments()].reverse()) {
      const view = resolveVariant(moment, viewer, narratives, this.now());
      const narrative = view.narrative;
      if (!narrative && view.state !== 'writing') {
        continue;
      }

      const entry = this.session.overlay().get(`${moment.id}:lower_third`);
      const club =
        moment.sideId === doc.home.club.id
          ? doc.home.club
          : moment.sideId === doc.away.club.id
            ? doc.away.club
            : null;
      cards.push({
        id: moment.id,
        club,
        headline: moment.headline,
        text: narrative?.renderedText ?? '',
        // English text is marked English: a fallback translation, and the "writing for you" placeholder.
        lang: narrative ? textLanguage(narrative) : 'en',
        chips: (entry?.chips ?? []).map((c) => ({ label: c.label, value: c.display })),
        pending: view.state === 'writing',
        note:
          view.state === 'not_involved'
            ? `${this.followed()?.name ?? 'Your player'} is not involved in this moment`
            : view.englishFallback
              ? 'Shown in English'
              : null,
      });
      if (cards.length === 5) {
        break;
      }
    }

    return cards;
  });

  protected readonly strip = computed(() => {
    const player = this.followed();
    const sheet = this.session.factSheet();
    if (!player || this.preferences().persona !== 'player_focus') {
      return null;
    }

    return {
      player,
      stats: PLAYER_STRIP.map((s) => ({
        label: s.label,
        value: formatFact(findFact(sheet, `player.${player.id}.${s.metric}`)),
      })),
      tags: String(findFact(sheet, `player.${player.id}.tags`)?.value ?? '')
        .split(',')
        .filter((t) => t.length > 0),
    };
  });

  constructor() {
    // At full time, and again when the language changes, the recap is fetched. It is spoken after the full-time
    // narratives are written, so a few tries are made before giving up; speech that is off simply shows no player.
    effect((onCleanup) => {
      const language = this.language();
      if (!this.fullTime()) {
        this.recap.set(null);
        return;
      }

      let cancelled = false;
      onCleanup(() => (cancelled = true));
      void this.loadRecap(this.matchId(), language, () => cancelled);
    });
  }

  ngOnInit(): void {
    const clock = setInterval(() => this.now.set(Date.now()), 1_000);
    this.destroyRef.onDestroy(() => clearInterval(clock));
    const prefs = this.preferences();
    void this.session.connect(this.matchId(), prefs);
  }

  /**
   * Reads the recap until it is ready, on the RECAP_POLL_MS schedule: the recap is spoken after the full-time
   * narratives, and a real voice per language can take a while. Speech that is off stops the reads at once.
   */
  private async loadRecap(
    matchId: string,
    language: string,
    cancelled: () => boolean,
  ): Promise<void> {
    // Only the chosen language's recap is offered: the last language's player goes as soon as the language changes.
    this.recap.set(null);
    this.recapError.set(null);
    let failure: unknown = null;
    for (const wait of [0, ...RECAP_POLL_MS]) {
      await new Promise((resolve) => setTimeout(resolve, wait));
      if (cancelled()) {
        return;
      }

      try {
        const result = await this.api.recap(matchId, language);
        if (cancelled()) {
          return;
        }

        failure = null;
        if (result.state === 'ready') {
          this.recap.set({ recap: result.recap, src: this.api.recapAudioUrl(matchId, language) });
          return;
        }

        if (result.state === 'off') {
          this.recap.set(null);
          return;
        }
      } catch (error) {
        // A failed read is tried again like a recap still being spoken; only the last failure is shown.
        failure = error;
      }
    }

    // A load the viewer has moved on from must not overwrite the current one.
    if (cancelled()) {
      return;
    }

    this.recap.set(null);
    this.recapError.set(failure ? toApiError(failure) : null);
  }

  protected setPersona(persona: string): void {
    void this.session.setViewer(this.store.update({ persona }));
  }

  protected setLanguage(language: string): void {
    void this.session.setViewer(this.store.update({ language }));
  }

  protected setClub(event: Event): void {
    void this.session.setViewer(
      this.store.update({ clubId: (event.target as HTMLSelectElement).value || null }),
    );
  }

  protected setPlayer(event: Event): void {
    void this.session.setViewer(
      this.store.update({ playerId: (event.target as HTMLSelectElement).value || null }),
    );
  }
}
