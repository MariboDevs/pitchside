import { Injectable, signal } from '@angular/core';
import { ViewerPreferences } from './contracts';

const STORAGE_KEY = 'pitchside.viewer';

/** The fan's persona, language and followed club or player, remembered on this device. */
@Injectable({ providedIn: 'root' })
export class ViewerStore {
  readonly preferences = signal<ViewerPreferences>(this.read());

  update(change: Partial<ViewerPreferences>): ViewerPreferences {
    const next = { ...this.preferences(), ...change };
    this.preferences.set(next);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    } catch {
      // Storage can be unavailable in a private window or an OBS browser source; the choice still applies now.
      return next;
    }

    return next;
  }

  private read(): ViewerPreferences {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (raw) {
        return JSON.parse(raw) as ViewerPreferences;
      }
    } catch {
      // Unreadable or unavailable storage falls back to the default below.
      return { persona: 'casual', language: 'en' };
    }

    return { persona: 'casual', language: 'en' };
  }
}
