import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Club } from '../../core/contracts';
import { Chip } from '../chip/chip';
import { Monogram } from '../monogram/monogram';

/** One displayed stat on a lower third. */
export interface LowerThirdChip {
  label: string;
  value: string;
}

/**
 * The lower third: an accent bar in the side's colour, its monogram, the headline and the narrative line, and at
 * most three stat chips. Sized in em, so the overlay scales it with the viewport and the viewer with its text.
 */
@Component({
  selector: 'app-lower-third',
  imports: [Chip, Monogram],
  templateUrl: './lower-third.html',
  styleUrl: './lower-third.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LowerThird {
  readonly club = input<Club | null>(null);
  readonly headline = input.required<string>();
  readonly text = input.required<string>();
  readonly chips = input<LowerThirdChip[]>([]);
  readonly fullWidth = input(false);
  readonly lang = input('en');
  /** The viewer's own variant is still being written: a quiet placeholder, never a spinner. */
  readonly pending = input(false);
  /** A short line under the text, such as "Not involved in this moment" or "Shown in English". */
  readonly note = input<string | null>(null);
}
