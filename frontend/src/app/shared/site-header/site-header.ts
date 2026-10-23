import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter, map } from 'rxjs';

/** A match surface the header links between. */
interface Surface {
  path: 'viewer' | 'control' | 'overlay';
  label: string;
  query?: Record<string, string>;
}

const SURFACES: Surface[] = [
  { path: 'viewer', label: 'Viewer' },
  { path: 'control', label: 'Control room' },
  { path: 'overlay', label: 'Overlay', query: { aspect: '16x9', preview: '1' } },
];

/**
 * The app's one navigation: the wordmark home, and on a match route the three surfaces of that match as tabs. It
 * reads the match id from the URL, so every route gets it without wiring. The overlay's own preview toolbar stands
 * in for it there, since a broadcast page draws graphics only.
 */
@Component({
  selector: 'app-site-header',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './site-header.html',
  styleUrl: './site-header.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteHeader {
  private readonly router = inject(Router);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  protected readonly surfaces = SURFACES;

  /** The match the route is about, or null on the match list. */
  protected readonly matchId = computed(
    () => /^\/(?:viewer|control|overlay)\/([^/?#]+)/.exec(this.url())?.[1] ?? null,
  );

  protected readonly hidden = computed(() => this.url().startsWith('/overlay/'));
}
