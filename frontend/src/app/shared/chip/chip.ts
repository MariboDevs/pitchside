import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** A stat chip: a fact's label above its value, tabular numerals. */
@Component({
  selector: 'app-chip',
  templateUrl: './chip.html',
  styleUrl: './chip.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Chip {
  readonly label = input.required<string>();
  readonly value = input.required<string>();
}
