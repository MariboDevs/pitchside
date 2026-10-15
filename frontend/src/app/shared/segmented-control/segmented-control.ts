import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** One choice in a segmented control. */
export interface Segment {
  id: string;
  label: string;
}

/** A row of mutually exclusive choices, as a radio group of buttons. */
@Component({
  selector: 'app-segmented-control',
  templateUrl: './segmented-control.html',
  styleUrl: './segmented-control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SegmentedControl {
  readonly label = input.required<string>();
  readonly options = input.required<readonly Segment[]>();
  readonly value = input.required<string>();
  readonly changed = output<string>();
}
