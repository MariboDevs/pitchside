import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { toApiError } from '../core/api-error';
import { StoredMatch } from '../core/contracts';
import { MatchApi } from '../core/match-api';
import { Monogram } from '../shared/monogram/monogram';

/** The match list, a replay starter and the links to each match's three surfaces. */
@Component({
  selector: 'app-home',
  imports: [ReactiveFormsModule, RouterLink, Monogram],
  templateUrl: './home.html',
  styleUrl: './home.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Home implements OnInit, OnDestroy {
  private readonly api = inject(MatchApi);
  private readonly fb = inject(FormBuilder);
  private timer: ReturnType<typeof setInterval> | undefined;

  readonly matches = signal<StoredMatch[] | null>(null);
  readonly error = signal<string | null>(null);
  readonly starting = signal(false);
  readonly copied = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    seed: [7, [Validators.required, Validators.min(1)]],
    speed: [1, [Validators.required, Validators.min(0)]],
  });

  ngOnInit(): void {
    void this.refresh();
    this.timer = setInterval(() => void this.refresh(), 5000);
  }

  ngOnDestroy(): void {
    clearInterval(this.timer);
  }

  async start(): Promise<void> {
    if (this.form.invalid) {
      return;
    }

    this.starting.set(true);
    this.error.set(null);
    try {
      const { seed, speed } = this.form.getRawValue();
      await this.api.startReplay(seed, speed);
      await this.refresh();
    } catch (error) {
      this.error.set(toApiError(error));
    } finally {
      this.starting.set(false);
    }
  }

  async copyOverlay(matchId: string): Promise<void> {
    const url = `${location.origin}/overlay/${matchId}?aspect=16x9`;
    try {
      await navigator.clipboard.writeText(url);
      this.copied.set(matchId);
    } catch {
      this.error.set(`Copy this URL into OBS: ${url}`);
    }
  }

  statusTone(status: string): string {
    return status === 'live'
      ? 'pill pill--ok'
      : status === 'paused' || status === 'scheduled'
        ? 'pill pill--waiting'
        : 'pill';
  }

  private async refresh(): Promise<void> {
    try {
      this.matches.set(await this.api.listMatches());
    } catch (error) {
      this.error.set(toApiError(error));
    }
  }
}
