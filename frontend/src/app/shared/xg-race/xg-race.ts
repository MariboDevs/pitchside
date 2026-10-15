import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatchDocument } from '../../core/contracts';
import { SeriesPoint } from '../../core/match-session';

const MINUTES = 95;

/**
 * The xG race: home's cumulative xG above a baseline, away's mirrored below, each filled at 20% opacity, goals as
 * dots. Values come from the fact series; the chart only maps them to coordinates. Vertical for 9:16.
 */
@Component({
  selector: 'app-xg-race',
  templateUrl: './xg-race.html',
  styleUrl: './xg-race.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class XgRace {
  readonly document = input.required<MatchDocument>();
  readonly series = input.required<SeriesPoint[]>();
  readonly orientation = input<'horizontal' | 'vertical'>('horizontal');

  protected readonly width = computed(() => (this.orientation() === 'horizontal' ? 1000 : 120));
  protected readonly height = computed(() => (this.orientation() === 'horizontal' ? 120 : 1000));

  private readonly scale = computed(() => {
    const max = Math.max(
      1,
      ...this.series().flatMap((p) => [p.values['home.xg'] ?? 0, p.values['away.xg'] ?? 0]),
    );
    return max * 1.15;
  });

  protected readonly home = computed(() => this.paths('home.xg', -1));
  protected readonly away = computed(() => this.paths('away.xg', 1));

  protected readonly goals = computed(() => {
    const dots: { x: number; y: number; side: 'home' | 'away' }[] = [];
    let previous = { home: 0, away: 0 };
    for (const p of this.series()) {
      const now = {
        home: p.values['match.score.home'] ?? 0,
        away: p.values['match.score.away'] ?? 0,
      };
      for (const side of ['home', 'away'] as const) {
        if (now[side] > previous[side]) {
          dots.push({
            ...this.point(p, p.values[`${side}.xg`] ?? 0, side === 'home' ? -1 : 1),
            side,
          });
        }
      }

      previous = now;
    }

    return dots;
  });

  private paths(key: string, direction: number): { line: string; area: string } {
    const points = this.series().map((p) => this.point(p, p.values[key] ?? 0, direction));
    if (points.length === 0) {
      return { line: '', area: '' };
    }

    const line = points
      .map((pt, i) => `${i === 0 ? 'M' : 'L'}${pt.x.toFixed(1)},${pt.y.toFixed(1)}`)
      .join(' ');
    const base = this.baseline(points[0]);
    const end = this.baseline(points[points.length - 1]);
    return { line, area: `M${base.x},${base.y} ${line.replace(/^M/, 'L')} L${end.x},${end.y} Z` };
  }

  private point(p: SeriesPoint, value: number, direction: number): { x: number; y: number } {
    const minute = Math.min(MINUTES, (p.period === 2 ? 45 : 0) + p.clockMs / 60000);
    const along =
      (minute / MINUTES) * (this.orientation() === 'horizontal' ? this.width() : this.height());
    const half = (this.orientation() === 'horizontal' ? this.height() : this.width()) / 2;
    const across = half + direction * (value / this.scale()) * (half - 4);
    return this.orientation() === 'horizontal' ? { x: along, y: across } : { x: across, y: along };
  }

  private baseline(pt: { x: number; y: number }): { x: number; y: number } {
    return this.orientation() === 'horizontal'
      ? { x: pt.x, y: this.height() / 2 }
      : { x: this.width() / 2, y: pt.y };
  }
}
