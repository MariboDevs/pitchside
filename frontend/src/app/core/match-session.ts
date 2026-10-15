import { HttpClient } from '@angular/common/http';
import { Injectable, OnDestroy, computed, inject, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { firstValueFrom } from 'rxjs';
import { apiBase } from './api-base';
import {
  ApprovalMode,
  ApprovalModeUpdate,
  FactSheet,
  MatchEvent,
  MatchSnapshot,
  MatchStatusUpdate,
  Moment,
  Narrative,
  OverlayEntry,
  StoredMatch,
  ViewerPreferences,
} from './contracts';
import { numberOf } from './format';

/** One point of the fact series: the xG race, the score and momentum as of an event. */
export interface SeriesPoint {
  asOfSeq: number;
  period: number;
  clockMs: number;
  values: Record<string, number>;
}

export type ConnectionState = 'connecting' | 'live' | 'reconnecting' | 'offline';

const SERIES_KEYS = [
  'home.xg',
  'away.xg',
  'match.score.home',
  'match.score.away',
  'match.momentum.home',
  'match.momentum.away',
];
const MAX_EVENTS = 300;

/**
 * Everything one screen knows about one match, as signals. It joins the match on the hub, applies the snapshot,
 * then applies every pushed message; on a reconnect it joins again and applies the new snapshot, so a client that
 * drops out is complete again after one message. Provided per route, never shared between matches.
 */
/** How far the overlay's presentation clock may run ahead of the last event. */
const PRESENTATION_CAP_MS = 20_000;

/** Back-off between join attempts while the hub stays connected. */
const JOIN_RETRY_MS = [1_000, 2_000, 5_000, 10_000];

@Injectable()
export class MatchSession implements OnDestroy {
  private readonly http = inject(HttpClient);
  private hub: HubConnection | null = null;
  private matchId = '';
  private viewer: ViewerPreferences | null = null;
  private disposed = false;
  private joining = false;
  private joinAttempt = 0;
  private presentationTimer: ReturnType<typeof setInterval> | null = null;
  private anchor = { period: 0, clockMs: 0, at: 0 };
  /** Overlay and approval pushes that arrive while a join is in flight, applied after its snapshot. */
  private pending: (() => void)[] = [];

  readonly connection = signal<ConnectionState>('connecting');
  readonly match = signal<StoredMatch | null>(null);
  readonly factSheet = signal<FactSheet | null>(null);
  readonly events = signal<MatchEvent[]>([]);
  readonly moments = signal<Moment[]>([]);
  readonly narratives = signal<ReadonlyMap<string, Narrative>>(new Map());
  readonly overlay = signal<ReadonlyMap<string, OverlayEntry>>(new Map());
  readonly approval = signal<ApprovalMode>('auto');
  readonly status = signal<MatchStatusUpdate | null>(null);
  readonly series = signal<SeriesPoint[]>([]);
  readonly error = signal<string | null>(null);

  readonly document = computed(() => this.match()?.document ?? null);

  /** The match clock: the latest event's period clock, as the hub reports it. The browser's clock is never used. */
  readonly clock = computed(() => {
    const last = this.events().at(-1);
    const status = this.status();
    const sheet = this.factSheet();
    const candidates = [
      last ? { period: last.period, clockMs: last.clockMs, seq: last.seq } : null,
      status ? { period: status.period, clockMs: status.clockMs, seq: status.lastSeq } : null,
      sheet ? { period: sheet.period, clockMs: sheet.clockMs, seq: sheet.asOfSeq } : null,
    ].filter((c): c is { period: number; clockMs: number; seq: number } => c !== null);
    const latest = candidates.sort((a, b) => b.seq - a.seq)[0];
    return latest ? { period: latest.period, clockMs: latest.clockMs } : { period: 0, clockMs: 0 };
  });

  /**
   * The overlay's presentation clock: the match clock, carried forward in real time between events (at most
   * PRESENTATION_CAP_MS past the last one), so a card due in a gap between events, or the full-time card after the
   * last event, still has its window. The score bug keeps the hub's clock.
   */
  readonly presentationClock = signal<{ period: number; clockMs: number }>({
    period: 0,
    clockMs: 0,
  });

  readonly score = computed(() => ({
    home: numberOf(this.factSheet(), 'match.score.home'),
    away: numberOf(this.factSheet(), 'match.score.away'),
  }));

  readonly live = computed(() => {
    const state = this.status()?.status ?? this.match()?.status;
    return state === 'live' || state === 'paused' || state === 'completed';
  });

  async connect(matchId: string, viewer: ViewerPreferences | null = null): Promise<void> {
    this.matchId = matchId;
    this.viewer = viewer;
    this.hub = new HubConnectionBuilder()
      .withUrl(`${apiBase()}/hubs/match`)
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();
    this.hub.on('Event', (e: MatchEvent) => this.applyEvent(e));
    this.hub.on('FactSheet', (s: FactSheet) => this.applySheet(s));
    this.hub.on('Moment', (m: Moment) =>
      this.moments.update((all) => (all.some((x) => x.id === m.id) ? all : [...all, m])),
    );
    this.hub.on('Narrative', (n: Narrative) =>
      this.narratives.update((all) => new Map(all).set(n.id, n)),
    );
    this.hub.on('OverlayEntry', (e: OverlayEntry) =>
      this.whenJoined(() => this.overlay.update((all) => new Map(all).set(e.id, e))),
    );
    this.hub.on('ApprovalMode', (u: ApprovalModeUpdate) =>
      this.whenJoined(() => this.approval.set(u.mode)),
    );
    this.hub.on('MatchStatus', (s: MatchStatusUpdate) => this.status.set(s));
    this.hub.onreconnecting(() => this.connection.set('reconnecting'));
    this.hub.onreconnected(() => void this.join());
    this.hub.onclose(() => {
      this.connection.set('offline');
      if (!this.disposed) {
        setTimeout(() => void this.start(), 3000);
      }
    });
    this.presentationTimer = setInterval(() => this.tickPresentation(), 250);
    await this.start();
  }

  /** Sends the viewer's persona and language; the next cards arrive in the new mode. */
  async setViewer(viewer: ViewerPreferences): Promise<void> {
    this.viewer = viewer;
    if (this.hub?.state === HubConnectionState.Connected) {
      await this.hub.invoke('SetViewer', this.matchId, viewer);
    }
  }

  /** The narrative for a moment in a persona and language, falling back to the base English narrative. */
  narrativeFor(momentId: string, persona: string, language: string): Narrative | undefined {
    const all = this.narratives();
    return all.get(`${momentId}:${persona}:${language}`) ?? all.get(`${momentId}:base:en`);
  }

  ngOnDestroy(): void {
    this.disposed = true;
    if (this.presentationTimer) {
      clearInterval(this.presentationTimer);
    }

    void this.hub?.stop();
  }

  private async start(): Promise<void> {
    if (this.disposed || !this.hub || this.hub.state !== HubConnectionState.Disconnected) {
      return;
    }

    try {
      this.connection.set('connecting');
      await this.hub.start();
      await this.join();
    } catch {
      this.connection.set('offline');
      if (!this.disposed) {
        setTimeout(() => void this.start(), 3000);
      }
    }
  }

  private async join(): Promise<void> {
    if (!this.hub) {
      return;
    }

    this.joining = true;
    try {
      if (this.viewer) {
        await this.hub.invoke('SetViewer', this.matchId, this.viewer);
      }

      const snapshot = await this.hub.invoke<MatchSnapshot>('JoinMatch', this.matchId);
      this.applySnapshot(snapshot);
      this.flushPending();
      this.connection.set('live');
      this.error.set(null);
      await Promise.all([this.loadRecentEvents(snapshot.match.lastSeq), this.loadSeries()]);
      this.joinAttempt = 0;
    } catch (error) {
      this.flushPending();
      const message = error instanceof Error ? error.message : 'Could not join the match.';
      this.error.set(message);
      this.connection.set('offline');
      // A missing match is final; anything else is retried while the hub stays connected.
      if (!/No match/.test(message)) {
        this.retryJoin();
      }
    }
  }

  private retryJoin(): void {
    const delay = JOIN_RETRY_MS[Math.min(this.joinAttempt, JOIN_RETRY_MS.length - 1)];
    this.joinAttempt++;
    setTimeout(() => {
      if (!this.disposed && this.hub?.state === HubConnectionState.Connected) {
        void this.join();
      }
    }, delay);
  }

  /** Runs an overlay or approval update now, or after the snapshot when a join is in flight. */
  private whenJoined(apply: () => void): void {
    if (this.joining) {
      this.pending.push(apply);
    } else {
      apply();
    }
  }

  private flushPending(): void {
    this.joining = false;
    const queued = this.pending;
    this.pending = [];
    queued.forEach((apply) => apply());
  }

  private tickPresentation(): void {
    const clock = this.clock();
    const now = performance.now();
    if (clock.period !== this.anchor.period || clock.clockMs !== this.anchor.clockMs) {
      this.anchor = { ...clock, at: now };
    }

    const ahead = Math.min(now - this.anchor.at, PRESENTATION_CAP_MS);
    const next = { period: clock.period, clockMs: clock.clockMs + Math.max(0, Math.round(ahead)) };
    const current = this.presentationClock();
    if (current.period !== next.period || current.clockMs !== next.clockMs) {
      this.presentationClock.set(next);
    }
  }

  private applySnapshot(snapshot: MatchSnapshot): void {
    this.match.set(snapshot.match);
    // A newer sheet can arrive while the join is in flight; the snapshot's older one must not roll it back.
    const current = this.factSheet();
    if (snapshot.factSheet && (!current || snapshot.factSheet.asOfSeq >= current.asOfSeq)) {
      this.factSheet.set(snapshot.factSheet);
    }

    // A live moment can arrive before the snapshot reply; keep it rather than overwrite it.
    this.moments.update((live) => {
      const known = new Set(snapshot.moments.map((m) => m.id));
      return [...snapshot.moments, ...live.filter((m) => !known.has(m.id))];
    });
    this.narratives.update((all) => {
      const next = new Map(all);
      snapshot.narratives.forEach((n) => next.set(n.id, n));
      return next;
    });
    this.overlay.set(new Map(snapshot.overlayEntries.map((e) => [e.id, e])));
    this.approval.set(snapshot.approvalMode);
  }

  private applyEvent(e: MatchEvent): void {
    this.events.update((all) => {
      if (all.length > 0 && all[all.length - 1].seq >= e.seq) {
        return all;
      }

      const next = [...all, e];
      return next.length > MAX_EVENTS ? next.slice(next.length - MAX_EVENTS) : next;
    });
  }

  private applySheet(sheet: FactSheet): void {
    this.factSheet.set(sheet);
    this.series.update((all) => [
      ...all,
      {
        asOfSeq: sheet.asOfSeq,
        period: sheet.period,
        clockMs: sheet.clockMs,
        values: Object.fromEntries(SERIES_KEYS.map((k) => [k, numberOf(sheet, k)])),
      },
    ]);
  }

  /** A late joiner gets the recent events from the store, so the feed and the pitch are not empty. */
  private async loadRecentEvents(lastSeq: number): Promise<void> {
    if (lastSeq <= 0) {
      return;
    }

    const fromSeq = Math.max(1, lastSeq - MAX_EVENTS + 1);
    const recent = await firstValueFrom(
      this.http.get<MatchEvent[]>(`${apiBase()}/api/v1/matches/${this.matchId}/events`, {
        params: { fromSeq, toSeq: lastSeq },
      }),
    );
    this.events.update((live) => {
      const merged = new Map(recent.map((e) => [e.seq, e]));
      live.forEach((e) => merged.set(e.seq, e));
      return [...merged.values()].sort((a, b) => a.seq - b.seq).slice(-MAX_EVENTS);
    });
  }

  private async loadSeries(): Promise<void> {
    const history = await firstValueFrom(
      this.http.get<SeriesPoint[]>(`${apiBase()}/api/v1/matches/${this.matchId}/facts/series`, {
        params: { keys: SERIES_KEYS.join(','), every: 10 },
      }),
    );
    this.series.update((live) => {
      const lastHistory = history.at(-1)?.asOfSeq ?? 0;
      return [...history, ...live.filter((p) => p.asOfSeq > lastHistory)];
    });
  }
}
