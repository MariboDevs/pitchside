import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Recap } from '../../core/contracts';

/** The spoken full-time recap: an audio player, and a note when English stands in for the chosen language. */
@Component({
  selector: 'app-recap-player',
  templateUrl: './recap-player.html',
  styleUrl: './recap-player.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecapPlayer {
  readonly recap = input.required<Recap>();
  readonly src = input.required<string>();
}
