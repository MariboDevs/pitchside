import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { toApiError } from '../core/api-error';
import { EventType, FactSheet, MatchEvent, Moment, OverlayEntry } from '../core/contracts';
import { findFact, formatClock, formatFact, splitTokens } from '../core/format';
import { MatchApi } from '../core/match-api';
import { MatchSession } from '../core/match-session';
import { Monogram } from '../shared/monogram/monogram';
import { Swimlane } from '../shared/swimlane/swimlane';

const FEED_TYPES: EventType[] = [
  'shot',
  'goal',
  'pass',
  'carry',
  'turnover',
  'foul',
  'card',
  'corner',
  'pressure',
  'substitution',
];
const FACT_GROUPS = ['match', 'home', 'away', 'player'] as const;

/**
 * The control room: the event feed, the moments with their narratives and the citation chain from each number
 * back to its events, the fact-checker's verdict, hold and air per card, the live fact sheet, the agent trace and
 * the replay controls. Keyboard: Space pause or resume, H hold, A air, J and K move, / filter the feed.
 */
@Component({
  selector: 'app-control',
  imports: [Monogram, Swimlane],
  templateUrl: './control.html',
  styleUrl: './control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [MatchSession],
  host: { '(document:keydown)': 'onKey($event)' },
})
export class Control implements OnInit {
  protected readonly session = inject(MatchSession);
  private readonly api = inject(MatchApi);
  private readonly filterInput = viewChild<ElementRef<HTMLInputElement>>('filter');

  readonly matchId = input.required<string>();

  protected readonly feedTypes = FEED_TYPES;
  protected readonly groups = FACT_GROUPS;
  protected readonly typeFilter = signal<string>('');
  protected readonly sideFilter = signal<string>('');
  protected readonly search = signal('');
  protected readonly selected = signal(0);
  protected readonly highlighted = signal<ReadonlySet<string>>(new Set());
  protected readonly sheets = signal<ReadonlyMap<number, FactSheet>>(new Map());
  protected readonly paused = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly names = computed(() => {
    const doc = this.session.document();
    const all = doc
      ? [...doc.home.starters, ...doc.home.bench, ...doc.away.starters, ...doc.away.bench]
      : [];
    return new Map(all.map((p) => [p.id, p.name]));
  });

  protected readonly feed = computed(() => {
    const type = this.typeFilter();
    const side = this.sideFilter();
    const term = this.search().trim().toLowerCase();
    return this.session
      .events()
      .filter((e) => e.type !== 'tracking')
      .filter((e) => (!type || e.type === type) && (!side || e.teamId === side))
      .filter(
        (e) => !term || `${e.type} ${this.playerName(e.playerId)}`.toLowerCase().includes(term),
      )
      .slice(-120)
      .reverse();
  });

  protected readonly moments = computed(() =>
    [...this.session.moments()].sort((a, b) => a.triggerSeq - b.triggerSeq),
  );
  protected readonly current = computed<Moment | undefined>(() => this.moments()[this.selected()]);

  protected readonly groupedFacts = computed(() => {
    const sheet = this.session.factSheet();
    return FACT_GROUPS.map((group) => ({
      group,
      facts: (sheet?.facts ?? []).filter((f) => f.key.startsWith(`${group}.`)),
    }));
  });

  ngOnInit(): void {
    void this.session.connect(this.matchId());
  }

  protected clockOf(period: number, clockMs: number): string {
    return formatClock(period, clockMs);
  }

  protected playerName(id: string | null | undefined): string {
    return id ? (this.names().get(id) ?? id) : '';
  }

  protected clubOf(teamId: string | null) {
    const doc = this.session.document();
    return doc && teamId
      ? teamId === doc.home.club.id
        ? doc.home.club
        : teamId === doc.away.club.id
          ? doc.away.club
          : null
      : null;
  }

  protected qualifier(e: MatchEvent): string {
    if (e.type === 'shot') {
      return `xG ${e.xg?.toFixed(2) ?? ''}${e.penalty ? ' pen' : ''}`;
    }

    if (e.type === 'pass') {
      return `difficulty ${e.difficulty?.toFixed(2) ?? ''}${e.completed === false ? ', lost' : ''}`;
    }

    if (e.type === 'goal') {
      return `${e.homeScore}-${e.awayScore}`;
    }

    return e.type === 'card' ? (e.colour ?? '') : '';
  }

  protected entryFor(moment: Moment): OverlayEntry | undefined {
    return this.session.overlay().get(`${moment.id}:lower_third`);
  }

  protected narrativeOf(moment: Moment) {
    return this.session.narrativeFor(moment.id, 'base', 'en');
  }

  protected parts(text: string) {
    return splitTokens(text);
  }

  /** A cited fact's value as of the moment's own sheet, fetched once when the card opens. */
  protected citedValue(moment: Moment, key: string): string {
    const sheet = this.sheets().get(moment.factSheetSeq);
    return sheet ? formatFact(findFact(sheet, key)) : '…';
  }

  protected citedSources(moment: Moment, key: string): string[] {
    return findFact(this.sheets().get(moment.factSheetSeq), key)?.derivedFrom ?? [];
  }

  protected async select(index: number): Promise<void> {
    this.selected.set(index);
    const moment = this.moments()[index];
    if (moment && !this.sheets().has(moment.factSheetSeq)) {
      try {
        const sheet = await this.api.factsAt(this.matchId(), moment.factSheetSeq);
        this.sheets.update((all) => new Map(all).set(moment.factSheetSeq, sheet));
      } catch (error) {
        this.error.set(toApiError(error));
      }
    }
  }

  protected highlight(ids: string[]): void {
    this.highlighted.set(new Set(ids));
  }

  protected async hold(moment: Moment | undefined): Promise<void> {
    const entry = moment ? this.entryFor(moment) : undefined;
    if (entry) {
      await this.run(() => this.api.hold(this.matchId(), entry.id));
    }
  }

  protected async air(moment: Moment | undefined): Promise<void> {
    const entry = moment ? this.entryFor(moment) : undefined;
    if (entry) {
      await this.run(() => this.api.air(this.matchId(), entry.id));
    }
  }

  protected async toggleApproval(): Promise<void> {
    await this.run(() =>
      this.api.setApproval(this.matchId(), this.session.approval() === 'auto' ? 'approve' : 'auto'),
    );
  }

  protected async togglePause(): Promise<void> {
    const pausing = !this.paused();
    await this.run(() =>
      pausing ? this.api.pause(this.matchId()) : this.api.resume(this.matchId()),
    );
    this.paused.set(pausing);
  }

  protected onKey(event: KeyboardEvent): void {
    const target = event.target as HTMLElement | null;
    if (target && ['INPUT', 'SELECT', 'TEXTAREA'].includes(target.tagName)) {
      return;
    }

    const last = this.moments().length - 1;
    switch (event.key) {
      case ' ':
        event.preventDefault();
        void this.togglePause();
        break;
      case 'h':
      case 'H':
        void this.hold(this.current());
        break;
      case 'a':
      case 'A':
        void this.air(this.current());
        break;
      case 'j':
      case 'J':
        void this.select(Math.min(last, this.selected() + 1));
        break;
      case 'k':
      case 'K':
        void this.select(Math.max(0, this.selected() - 1));
        break;
      case '/':
        event.preventDefault();
        this.filterInput()?.nativeElement.focus();
        break;
      default:
        break;
    }
  }

  protected readValue(event: Event): string {
    return (event.target as HTMLInputElement | HTMLSelectElement).value;
  }

  private async run(action: () => Promise<unknown>): Promise<void> {
    this.error.set(null);
    try {
      await action();
    } catch (error) {
      this.error.set(toApiError(error));
    }
  }
}
