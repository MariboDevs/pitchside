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
import { LANGUAGES, Recap } from '../../core/contracts';
import { MatchApi } from '../../core/match-api';

/**
 * The control room's view of the spoken full-time recap (WP10-R3): for each language, the voice, whether English
 * stands in, and the SSML that was synthesised. Recaps are spoken after the full-time narratives, so the read is
 * tried a few times.
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
  protected readonly state = signal<'loading' | 'ready' | 'none'>('loading');
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    let destroyed = false;
    this.destroyRef.onDestroy(() => (destroyed = true));
    void this.load(() => destroyed);
  }

  private async load(destroyed: () => boolean): Promise<void> {
    for (let attempt = 0; attempt < 10 && !destroyed(); attempt++) {
      try {
        const found = await Promise.all(LANGUAGES.map((l) => this.api.recap(this.matchId(), l.id)));
        const recaps = found.filter((r): r is Recap => r !== null);
        this.error.set(null);
        if (recaps.length > 0) {
          this.recaps.set(recaps);
          this.state.set('ready');
          return;
        }
      } catch (error) {
        this.error.set(toApiError(error));
      }

      await new Promise((resolve) => setTimeout(resolve, 2_000));
    }

    if (!destroyed()) {
      this.state.set('none');
    }
  }
}
