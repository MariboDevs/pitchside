import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatchDocument } from '../../core/contracts';

/** A 6 px bar split by each side's momentum share, in the clubs' colours, eased over 600 ms. */
@Component({
  selector: 'app-momentum-ribbon',
  templateUrl: './momentum-ribbon.html',
  styleUrl: './momentum-ribbon.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MomentumRibbon {
  readonly document = input.required<MatchDocument>();
  readonly home = input.required<number>();
  readonly away = input.required<number>();
  protected readonly homeShare = computed(() => {
    const total = this.home() + this.away();
    return total <= 0 ? 50 : (this.home() / total) * 100;
  });
}
