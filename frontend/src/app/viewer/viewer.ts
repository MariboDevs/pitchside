import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input } from '@angular/core';
import { Club, LANGUAGES, PERSONAS, Player } from '../core/contracts';
import { findFact, formatFact } from '../core/format';
import { MatchSession } from '../core/match-session';
import { resolveVariant } from '../core/variant';
import { ViewerStore } from '../core/viewer-store';
import { LowerThird, LowerThirdChip } from '../shared/lower-third/lower-third';
import { Pitch } from '../shared/pitch/pitch';
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
  imports: [LowerThird, Pitch, ScoreBug, SegmentedControl],
  templateUrl: './viewer.html',
  styleUrl: './viewer.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [MatchSession],
})
export class Viewer implements OnInit {
  protected readonly session = inject(MatchSession);
  private readonly store = inject(ViewerStore);

  readonly matchId = input.required<string>();

  protected readonly personas = PERSONAS;
  protected readonly languages = LANGUAGES;
  protected readonly preferences = this.store.preferences;

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
      const view = resolveVariant(moment, viewer, narratives);
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
        lang: narrative?.language ?? viewer.language,
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

  ngOnInit(): void {
    const prefs = this.preferences();
    void this.session.connect(this.matchId(), prefs);
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
