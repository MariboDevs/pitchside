// Routes and viewports for the ui-invariants layout scan. Every route the app declares is listed here; the
// parameterised ones resolve against the replayed seed-7 match.
const config = {
  baseUrl: 'http://localhost:4200',
  viewports: [
    { width: 360, height: 780 },
    { width: 768, height: 1024 },
    { width: 1280, height: 900 },
  ],
  routes: [
    { path: '/', ready: '[data-testid="match-row"]' },
    { path: '/viewer/:matchId', ready: '[data-testid="pitch"]' },
    { path: '/control/:matchId', ready: '[data-testid="feed"]' },
    { path: '/overlay/:matchId', query: '?aspect=16x9', ready: '[data-testid="overlay-stage"]' },
  ],
  routeParams: { matchId: 'MER-7' },
};

export default config;
