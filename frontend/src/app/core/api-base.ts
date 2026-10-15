declare global {
  interface Window {
    __APP_API_BASE__?: string;
  }
}

/** The API origin: injected at run time under Aspire, otherwise the same origin (the dev proxy or the reverse proxy). */
export function apiBase(): string {
  return window.__APP_API_BASE__ ?? '';
}
