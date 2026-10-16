import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  input,
  signal,
} from '@angular/core';
import { toApiError } from '../../core/api-error';
import { LANGUAGES, RECAP_POLL_MS, Recap } from '../../core/contracts';
import { MatchApi } from '../../core/match-api';

/**
 * The control room's view of the spoken full-time recap (WP10-R3): for each language, the voice, whether English
 * stands in, and the SSML that was synthesised. Recaps are spoken after the full-time narratives, one language
 * after another, so the reads go on until every language is there.
 */
@Component({
  selector: 'app-recap-ssml',
  templateUrl: './recap-ssml.html',
  styleUrl: './recap-ssml.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecapSsml implements OnInit {
  private readonly api = inject(MatchApi);
  private readonly destroyRef = inject(DestroyRef);

  readonly matchId = input.required<string>();

  protected readonly recaps = signal<Recap[]>([]);
  protected readonly state = signal<'loading' | 'ready' | 'none' | 'off'>('loading');
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    let destroyed = false;
    this.destroyRef.onDestroy(() => (destroyed = true));
    void this.load(() => destroyed);
  }

  /**
   * Reads every language's recap until all four are there, on the RECAP_POLL_MS schedule: languages are spoken one
   * after another, so the first read can find some and not others. What has arrived is shown as it arrives.
   */
  private async load(destroyed: () => boolean): Promise<void> {
    const found = new Map<string, Recap>();
    for (const wait of [0, ...RECAP_POLL_MS]) {
      await new Promise((resolve) => setTimeout(resolve, wait));
      if (destroyed()) {
        return;
      }

      try {
        const missing = LANGUAGES.filter((l) => !found.has(l.id));
        const results = await Promise.all(missing.map((l) => this.api.recap(this.matchId(), l.id)));
        this.error.set(null);
        if (results.some((r) => r.state === 'off')) {
          this.state.set('off');
          return;
        }

        for (const result of results) {
          if (result.state === 'ready') {
            found.set(result.recap.language, result.recap);
          }
        }

        if (found.size > 0) {
          this.recaps.set(LANGUAGES.flatMap((l) => found.get(l.id) ?? []));
          this.state.set('ready');
        }

        if (found.size === LANGUAGES.length) {
          return;
        }
      } catch (error) {
        this.error.set(toApiError(error));
      }
    }

    if (found.size === 0) {
      this.state.set('none');
    }
  }
}
