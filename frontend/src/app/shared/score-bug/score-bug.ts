import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatchDocument } from '../../core/contracts';
import { formatClock } from '../../core/format';
import { Monogram } from '../monogram/monogram';

/** The always-on score bug: both monograms, the score and the match clock from the hub. */
@Component({
  selector: 'app-score-bug',
  imports: [Monogram],
  templateUrl: './score-bug.html',
  styleUrl: './score-bug.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScoreBug {
  readonly document = input.required<MatchDocument>();
  readonly home = input.required<number>();
  readonly away = input.required<number>();
  readonly period = input.required<number>();
  readonly clockMs = input.required<number>();
  readonly variant = input<'broadcast' | 'app'>('broadcast');
  protected readonly clock = computed(() => formatClock(this.period(), this.clockMs()));
}
