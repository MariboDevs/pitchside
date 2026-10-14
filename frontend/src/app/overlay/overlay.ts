import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  computed,
  effect,
  inject,
  input,
} from '@angular/core';
import { OverlayEntry } from '../core/contracts';
import { numberOf } from '../core/format';
import { MatchSession } from '../core/match-session';
import { LowerThird, LowerThirdChip } from '../shared/lower-third/lower-third';
import { MomentumRibbon } from '../shared/momentum-ribbon/momentum-ribbon';
import { ScoreBug } from '../shared/score-bug/score-bug';
import { XgRace } from '../shared/xg-race/xg-race';

/** A period clock position. */
interface ClockPosition {
  period: number;
  clockMs: number;
}

/**
 * The broadcast overlay for a vision mixer or an OBS browser source: a transparent page with graphics only. It
 * draws the lower third the timeline has on air at the match clock, the score bug, the momentum ribbon and the xG
 * race. `?at=2:1322000` pins the clock to a period and millisecond, so a replay places entries identically.
 */
@Component({
  selector: 'app-overlay',
  imports: [LowerThird, MomentumRibbon, ScoreBug, XgRace],
  templateUrl: './overlay.html',
  styleUrl: './overlay.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [MatchSession],
})
export class Overlay implements OnInit, OnDestroy {
  private readonly document = inject(DOCUMENT);
  protected readonly session = inject(MatchSession);

  readonly matchId = input.required<string>();
  readonly aspect = input<string | undefined>('16x9');
  readonly persona = input<string | undefined>();
  readonly lang = input<string | undefined>();
  readonly safe = input<string | undefined>();
  readonly at = input<string | undefined>();

  protected readonly vertical = computed(() => this.aspect() === '9x16');
  protected readonly safeArea = computed(() => {
    const value = Number(this.safe());
    return Number.isFinite(value) && value >= 0 && value <= 0.2 ? `${value * 100}%` : '5%';
  });

  private readonly pinned = computed<ClockPosition | null>(() => {
    const match = /^([12]):(\d+)$/.exec(this.at() ?? '');
    return match ? { period: Number(match[1]), clockMs: Number(match[2]) } : null;
  });

  /** Where the timeline is: the presentation clock, which keeps moving between events so every card gets its window. */
  protected readonly clock = computed<ClockPosition>(
    () => this.pinned() ?? this.session.presentationClock(),
  );

  /** What the score bug shows: the hub's match clock, which stops when the match pauses or ends. */
  protected readonly matchClock = computed<ClockPosition>(
    () => this.pinned() ?? this.session.clock(),
  );

  /** The fact series up to the clock: the xG race and, when pinned, the score and momentum at that moment. */
  protected readonly series = computed(() => {
    const at = this.clock();
    return this.session
      .series()
      .filter((p) => p.period < at.period || (p.period === at.period && p.clockMs <= at.clockMs));
  });

  private readonly pointAtClock = computed(() =>
    this.pinned() ? this.series().at(-1) : undefined,
  );

  protected readonly score = computed(() => {
    const point = this.pointAtClock();
    return point
      ? { home: point.values['match.score.home'] ?? 0, away: point.values['match.score.away'] ?? 0 }
      : this.session.score();
  });

  protected readonly momentum = computed(() => {
    const point = this.pointAtClock();
    const sheet = this.session.factSheet();
    return point
      ? {
          home: point.values['match.momentum.home'] ?? 0.5,
          away: point.values['match.momentum.away'] ?? 0.5,
        }
      : sheet
        ? {
            // Zero is a real share; the ribbon itself splits evenly when both are zero.
            home: numberOf(sheet, 'match.momentum.home'),
            away: numberOf(sheet, 'match.momentum.away'),
          }
        : { home: 0.5, away: 0.5 };
  });

  /** The lower thirds on air at the clock. The scheduler guarantees at most one; the renderer checks it. */
  private readonly onAir = computed<OverlayEntry[]>(() => {
    const at = this.clock();
    return [...this.session.overlay().values()].filter(
      (e) =>
        e.layer === 'lower_third' &&
        (e.state === 'scheduled' || e.state === 'aired') &&
        e.period === at.period &&
        e.inAtMs <= at.clockMs &&
        at.clockMs < e.outAtMs,
    );
  });

  protected readonly card = computed(() => {
    const entry = this.onAir()[0];
    if (!entry) {
      return null;
    }

    const moment = this.session.moments().find((m) => m.id === entry.momentId);
    const narrative = this.session.narrativeFor(
      entry.momentId,
      this.persona() ?? 'base',
      this.lang() ?? 'en',
    );
    const doc = this.session.document();
    if (!moment || !narrative || !doc) {
      return null;
    }

    const club =
      moment.sideId === doc.home.club.id
        ? doc.home.club
        : moment.sideId === doc.away.club.id
          ? doc.away.club
          : null;
    const chips: LowerThirdChip[] = entry.chips.map((c) => ({ label: c.label, value: c.display }));
    return {
      id: entry.id,
      club,
      headline: moment.headline,
      text: narrative.renderedText,
      chips,
      fullWidth: entry.fullWidth,
      lang: narrative.language,
    };
  });

  constructor() {
    effect(() => {
      const count = this.onAir().length;
      if (count > 1) {
        console.error(`Overlay invariant broken: ${count} lower thirds on air at once.`);
      }
    });
  }

  ngOnInit(): void {
    this.document.documentElement.classList.add('overlay-route');
    void this.session.connect(this.matchId(), {
      persona: this.persona() ?? 'base',
      language: this.lang() ?? 'en',
    });
  }

  ngOnDestroy(): void {
    this.document.documentElement.classList.remove('overlay-route');
  }
}
