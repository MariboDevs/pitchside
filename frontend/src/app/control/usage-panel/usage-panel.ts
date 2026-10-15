import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { UsageCount, UsageReport } from '../../core/contracts';
import { MatchApi } from '../../core/match-api';

const LABELS: Record<UsageCount['name'], string> = {
  replays: 'Replays started this hour (this address)',
  narratives: 'Moments narrated this hour',
  spokenLines: 'Spoken lines this hour',
  connections: 'Live connections (this address)',
};

/** How often the counts are read again. */
const REFRESH_MS = 15_000;

/**
 * The usage guard's counts against its limits (WP-07): replays per address, moments narrated and spoken lines per
 * host, live connections per address, and replays running against the cap. Read every 15 seconds.
 */
@Component({
  selector: 'app-usage-panel',
  templateUrl: './usage-panel.html',
  styleUrl: './usage-panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsagePanel implements OnInit {
  private readonly api = inject(MatchApi);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly usage = signal<UsageReport | null>(null);

  ngOnInit(): void {
    void this.load();
    const timer = setInterval(() => void this.load(), REFRESH_MS);
    this.destroyRef.onDestroy(() => clearInterval(timer));
  }

  protected label(count: UsageCount): string {
    return LABELS[count.name] ?? count.name;
  }

  protected full(count: UsageCount): boolean {
    return count.used >= count.limit;
  }

  private async load(): Promise<void> {
    try {
      this.usage.set(await this.api.usage());
    } catch {
      // The panel is advisory: when the counts cannot be read it keeps the last ones, and the next refresh tries again.
      return;
    }
  }
}
