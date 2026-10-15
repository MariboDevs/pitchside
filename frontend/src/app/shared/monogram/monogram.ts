import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Club } from '../../core/contracts';
import { monogramOf } from '../../core/format';

/** A club's monogram: two or three letters in its own colours, generated, never an image. */
@Component({
  selector: 'app-monogram',
  templateUrl: './monogram.html',
  styleUrl: './monogram.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Monogram {
  readonly club = input.required<Club>();
  readonly size = input<'sm' | 'md' | 'lg'>('md');
  protected readonly letters = computed(() => monogramOf(this.club().shortName));
}
