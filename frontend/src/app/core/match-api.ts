import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { apiBase } from './api-base';
import {
  ApprovalMode,
  FactSheet,
  MatchEvent,
  OverlayEntry,
  ReplayStarted,
  StoredMatch,
} from './contracts';

/** The match REST API under /api/v1. */
@Injectable({ providedIn: 'root' })
export class MatchApi {
  private readonly http = inject(HttpClient);

  listMatches(): Promise<StoredMatch[]> {
    return firstValueFrom(this.http.get<StoredMatch[]>(`${apiBase()}/api/v1/matches`));
  }

  startReplay(seed: number, speed: number): Promise<ReplayStarted> {
    return firstValueFrom(
      this.http.post<ReplayStarted>(`${apiBase()}/api/v1/replays`, { seed, speed }),
    );
  }

  factsAt(matchId: string, atSeq: number): Promise<FactSheet> {
    return firstValueFrom(
      this.http.get<FactSheet>(`${apiBase()}/api/v1/matches/${matchId}/facts`, {
        params: { atSeq },
      }),
    );
  }

  events(matchId: string, fromSeq: number, toSeq: number): Promise<MatchEvent[]> {
    return firstValueFrom(
      this.http.get<MatchEvent[]>(`${apiBase()}/api/v1/matches/${matchId}/events`, {
        params: { fromSeq, toSeq },
      }),
    );
  }

  pause(matchId: string): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${apiBase()}/api/v1/replays/${matchId}/pause`, null),
    );
  }

  resume(matchId: string): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${apiBase()}/api/v1/replays/${matchId}/resume`, null),
    );
  }

  hold(matchId: string, entryId: string): Promise<OverlayEntry> {
    return firstValueFrom(
      this.http.post<OverlayEntry>(
        `${apiBase()}/api/v1/matches/${matchId}/overlay/${encodeURIComponent(entryId)}/hold`,
        null,
      ),
    );
  }

  air(matchId: string, entryId: string): Promise<OverlayEntry> {
    return firstValueFrom(
      this.http.post<OverlayEntry>(
        `${apiBase()}/api/v1/matches/${matchId}/overlay/${encodeURIComponent(entryId)}/air`,
        null,
      ),
    );
  }

  setApproval(matchId: string, mode: ApprovalMode): Promise<unknown> {
    return firstValueFrom(
      this.http.post(`${apiBase()}/api/v1/matches/${matchId}/approval`, { mode }),
    );
  }
}
