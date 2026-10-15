import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { toApiError } from '../core/api-error';
import {
  AuditRecord,
  EventType,
  FactSheet,
  MatchEvent,
  Moment,
  Narrative,
  OverlayEntry,
} from '../core/contracts';
import { findFact, formatClock, formatFact, splitTokens } from '../core/format';
import { MatchApi } from '../core/match-api';
import { textLanguage } from '../core/variant';
import { MatchSession } from '../core/match-session';
import { Monogram } from '../shared/monogram/monogram';
import { Swimlane } from '../shared/swimlane/swimlane';
import { UsagePanel } from './usage-panel/usage-panel';

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
  imports: [Monogram, Swimlane, UsagePanel],
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
  /** From the match's own lifecycle, so a replay paused elsewhere, or before this page opened, reads as paused. */
  protected readonly paused = computed(
    () => (this.session.status()?.status ?? this.session.match()?.status) === 'paused',
  );
  /** Cited events that are no longer in the live feed, fetched when a moment is inspected. */
  protected readonly evidence = signal<ReadonlyMap<string, MatchEvent>>(new Map());
  private readonly loadingSheets = new Set<number>();
  /** Every model call of the match read so far (WP-07), grouped by moment, and where the next read starts. */
  private readonly audit = signal<ReadonlyMap<string, AuditRecord[]>>(new Map());
  private auditCursor: string | null = null;
  private auditLoading = false;
  private readonly auditTried = new Set<string>();
  /** Moments whose cited events have been gathered into the evidence cache, or are being. */
  private readonly evidenceFor = new Set<string>();
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
    // The control room is the one viewer that receives every persona variant in every language (WP06-R2).
    void this.session.connect(this.matchId(), { persona: 'all', language: 'en' });
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

  protected variantsOf(moment: Moment) {
    return this.session.variantsOf(moment.id).filter((n) => n.id !== `${moment.id}:base:en`);
  }

  /** The language the variant's text is in: English when its translation fell back. */
  protected langOf(n: Narrative): string {
    return textLanguage(n);
  }

  protected variantLabel(n: { persona: string; subject: string | null; language: string }): string {
    return `${n.persona.replace('_', ' ')}${n.subject ? ` (${n.subject})` : ''} · ${n.language}`;
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

  /** The cited events a hover names that the live feed no longer shows, in match order. */
  protected readonly offFeed = computed(() => {
    const inFeed = new Set(this.feed().map((e) => e.id));
    return [...this.highlighted()]
      .filter((id) => !inFeed.has(id))
      .map((id) => this.evidence().get(id))
      .filter((e): e is MatchEvent => !!e)
      .sort((a, b) => a.seq - b.seq);
  });

  constructor() {
    // The selected moment is expanded, the first one included, so its sheet loads as soon as it exists.
    effect(() => {
      const moment = this.current();
      if (moment && !this.sheets().has(moment.factSheetSeq)) {
        untracked(() => void this.loadSheet(moment));
      }
    });

    // The trace swimlane shows each stage's model calls once the moment's narrative exists; the audit is read on from
    // where the last read stopped, once per moment, so a moment whose calls are not yet stored is not polled.
    effect(() => {
      const moment = this.current();
      if (
        moment &&
        this.narrativeOf(moment) &&
        !this.audit().has(moment.id) &&
        !this.auditTried.has(moment.id)
      ) {
        this.auditTried.add(moment.id);
        untracked(() => void this.loadAudit());
      }
    });

    // Its cited events need both the sheet and the narrative, which can arrive in either order on a live match.
    effect(() => {
      const moment = this.current();
      const sheet = moment ? this.sheets().get(moment.factSheetSeq) : undefined;
      const narrative = moment ? this.narrativeOf(moment) : undefined;
      if (moment && sheet && narrative && !this.evidenceFor.has(moment.id)) {
        this.evidenceFor.add(moment.id);
        untracked(() => void this.loadEvidence(moment, sheet));
      }
    });
  }

  protected select(index: number): void {
    this.selected.set(index);
  }

  protected callsOf(moment: Moment): AuditRecord[] {
    return this.audit().get(moment.id) ?? [];
  }

  protected groundedness(n: Narrative): string {
    return n.evaluation.groundedness === null ? 'pending' : n.evaluation.groundedness.toFixed(2);
  }

  protected highlight(ids: string[]): void {
    this.highlighted.set(new Set(ids));
  }

  private async loadSheet(moment: Moment): Promise<void> {
    if (this.loadingSheets.has(moment.factSheetSeq)) {
      return;
    }

    this.loadingSheets.add(moment.factSheetSeq);
    try {
      const sheet = await this.api.factsAt(this.matchId(), moment.factSheetSeq);
      this.sheets.update((all) => new Map(all).set(moment.factSheetSeq, sheet));
    } catch (error) {
      this.error.set(toApiError(error));
    } finally {
      this.loadingSheets.delete(moment.factSheetSeq);
    }
  }

  /** Reads the match's audit records from the cursor on, every page, and groups them by moment. */
  private async loadAudit(): Promise<void> {
    if (this.auditLoading) {
      return;
    }

    this.auditLoading = true;
    try {
      const read: AuditRecord[] = [];
      let page = await this.api.audit(this.matchId(), this.auditCursor);
      read.push(...page.items);
      while (page.next) {
        page = await this.api.audit(this.matchId(), page.next);
        read.push(...page.items);
      }

      this.auditCursor = read.at(-1)?.id ?? this.auditCursor;
      this.audit.update((all) => {
        const next = new Map(all);
        read.forEach((r) => next.set(r.momentId, [...(next.get(r.momentId) ?? []), r]));
        return next;
      });
    } catch (error) {
      this.error.set(toApiError(error));
    } finally {
      this.auditLoading = false;
    }
  }

  /**
   * Gathers the events the moment's narrative cites into the evidence cache: those the session still holds are kept
   * from it, so they survive its buffer and a filtered or trimmed feed, and the rest are fetched in one range.
   */
  private async loadEvidence(moment: Moment, sheet: FactSheet): Promise<void> {
    const narrative = this.narrativeOf(moment);
    const keys = new Set(narrative?.sentences.flatMap((s) => s.citations) ?? []);
    const cited = new Set(sheet.facts.filter((f) => keys.has(f.key)).flatMap((f) => f.derivedFrom));
    const held = this.session.events().filter((e) => cited.has(e.id));
    this.evidence.update((all) => {
      const next = new Map(all);
      held.forEach((e) => next.set(e.id, e));
      return next;
    });

    const missing = [...cited].filter((id) => !this.evidence().has(id));
    const seqs = missing
      .map((id) => Number(id.slice(id.lastIndexOf('-') + 1)))
      .filter((n) => n > 0);
    if (seqs.length === 0) {
      return;
    }

    const wanted = new Set(missing);
    try {
      const events = await this.api.events(this.matchId(), Math.min(...seqs), Math.max(...seqs));
      this.evidence.update((all) => {
        const next = new Map(all);
        events.filter((e) => wanted.has(e.id)).forEach((e) => next.set(e.id, e));
        return next;
      });
    } catch (error) {
      this.evidenceFor.delete(moment.id);
      this.error.set(toApiError(error));
    }
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
    // The button follows the match status the hub pushes back, not a local guess.
    await this.run(() =>
      this.paused() ? this.api.resume(this.matchId()) : this.api.pause(this.matchId()),
    );
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
        this.select(Math.min(last, this.selected() + 1));
        break;
      case 'k':
      case 'K':
        this.select(Math.max(0, this.selected() - 1));
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
