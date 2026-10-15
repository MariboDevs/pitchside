import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatchDocument, MatchEvent } from '../../core/contracts';

/**
 * The pitch in SVG with the last 20 events' locations as fading marks in club colours and the ball where the
 * latest event left it. Coordinates arrive in the attacking side's frame; the away side's are mirrored so home
 * always attacks right. A goal flashes the pitch edge in the scorer's colour.
 */
@Component({
  selector: 'app-pitch',
  templateUrl: './pitch.html',
  styleUrl: './pitch.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Pitch {
  readonly document = input.required<MatchDocument>();
  readonly events = input.required<MatchEvent[]>();

  protected readonly marks = computed(() => {
    const recent = this.events()
      .filter((e) => e.type !== 'tracking' && e.type !== 'substitution')
      .slice(-20);
    return recent.map((e, i) => ({
      ...this.place(e),
      opacity: (i + 1) / recent.length,
      home: e.teamId === this.document().home.club.id,
    }));
  });

  protected readonly ball = computed(() => this.marks().at(-1) ?? null);

  protected readonly flash = computed(() => {
    const last = this.events()
      .filter((e) => e.type !== 'tracking')
      .at(-1);
    if (last?.type !== 'goal') {
      return null;
    }

    return last.teamId === this.document().home.club.id
      ? this.document().home.club.primaryColour
      : this.document().away.club.primaryColour;
  });

  private place(e: MatchEvent): { x: number; y: number } {
    const home = e.teamId === this.document().home.club.id;
    return home
      ? { x: e.location.x, y: e.location.y }
      : { x: 105 - e.location.x, y: 68 - e.location.y };
  }
}
