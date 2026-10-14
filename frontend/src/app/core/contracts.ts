// TypeScript twins of the C# contracts in src/Pitchside.Contracts, as they arrive over HTTP and SignalR
// (camelCase properties, snake_case enum values). The frontend computes nothing but layout from them.

export type EventType =
  | 'kick_off'
  | 'pass'
  | 'carry'
  | 'shot'
  | 'goal'
  | 'turnover'
  | 'out_of_play'
  | 'corner'
  | 'foul'
  | 'card'
  | 'substitution'
  | 'pressure'
  | 'half_end'
  | 'full_time'
  | 'tracking';

export interface PitchLocation {
  x: number;
  y: number;
}

export interface MatchEvent {
  id: string;
  matchId: string;
  seq: number;
  period: number;
  clockMs: number;
  wallTimeUtc: string;
  type: EventType;
  teamId: string;
  playerId: string | null;
  location: PitchLocation;
  possessionId: number;
  endLocation?: PitchLocation;
  difficulty?: number;
  completed?: boolean;
  xg?: number;
  outcome?: string;
  penalty?: boolean;
  homeScore?: number;
  awayScore?: number;
  colour?: 'yellow' | 'red';
  playerOnId?: string;
  playerOffId?: string;
}

export interface Club {
  id: string;
  name: string;
  shortName: string;
  primaryColour: string;
  secondaryColour: string;
}

export interface Player {
  id: string;
  name: string;
  shirtNumber: number;
  position: 'goalkeeper' | 'defender' | 'midfielder' | 'forward';
  workRate: number;
}

export interface TeamSheet {
  club: Club;
  formation: string;
  starters: Player[];
  bench: Player[];
}

export interface MatchDocument {
  schemaVersion: string;
  matchId: string;
  seed: number;
  competition: string;
  venue: string;
  kickoffUtc: string;
  home: TeamSheet;
  away: TeamSheet;
}

export type MatchStatus = 'scheduled' | 'live' | 'paused' | 'completed' | 'stopped';

export interface StoredMatch {
  document: MatchDocument;
  status: MatchStatus;
  lastSeq: number;
  createdUtc: string;
  matchId: string;
}

export interface Fact {
  key: string;
  value: number | string;
  unit?: string;
  precision: number;
  derivedFrom: string[];
  window?: { fromSeq: number; toSeq: number };
}

export interface FactSheet {
  matchId: string;
  asOfSeq: number;
  period: number;
  clockMs: number;
  facts: Fact[];
}

export interface Moment {
  id: string;
  matchId: string;
  type: string;
  priority: number;
  period: number;
  clockMs: number;
  triggerSeq: number;
  window: { fromSeq: number; toSeq: number };
  sideId: string | null;
  playerIds: string[];
  evidence: { eventIds: string[]; factKeys: string[] };
  headline: string;
  factSheetSeq: number;
}

export type NarrativeStatus = 'draft' | 'verified' | 'rejected' | 'fallback';

export interface NarrativeSentence {
  text: string;
  citations: string[];
}

export interface Narrative {
  id: string;
  matchId: string;
  momentId: string;
  persona: string;
  /** The club id of a club_fan variant or the player id of a player_focus one. */
  subject: string | null;
  language: string;
  factSheetSeq: number;
  sentences: NarrativeSentence[];
  renderedText: string;
  status: NarrativeStatus;
  provenance: {
    provider: string;
    model: string;
    promptVersion: string;
    attempts: number;
    generatedUtc: string;
    fallback: boolean;
  };
  evaluation: { citationCoverage: number; groundedness: number | null };
}

export type OverlayState = 'scheduled' | 'held' | 'aired' | 'expired';

export interface OverlayChip {
  label: string;
  factKey: string;
  display: string;
}

export interface OverlayEntry {
  id: string;
  matchId: string;
  momentId: string;
  layer: 'score_bug' | 'lower_third' | 'stat_chip' | 'ribbon';
  slot: 'top_left' | 'bottom' | 'right_rail';
  priority: number;
  inAtMs: number;
  outAtMs: number;
  period: number;
  narrativeId: string;
  chips: OverlayChip[];
  state: OverlayState;
  fullWidth: boolean;
}

export type ApprovalMode = 'auto' | 'approve';

export interface ApprovalModeUpdate {
  matchId: string;
  mode: ApprovalMode;
}

export interface MatchStatusUpdate {
  matchId: string;
  status: MatchStatus;
  lastSeq: number;
  period: number;
  clockMs: number;
}

export interface MatchSnapshot {
  match: StoredMatch;
  factSheet: FactSheet | null;
  moments: Moment[];
  narratives: Narrative[];
  overlayEntries: OverlayEntry[];
  approvalMode: ApprovalMode;
}

export interface ViewerPreferences {
  persona: string;
  language: string;
  clubId?: string | null;
  playerId?: string | null;
}

export interface ReplayStarted {
  matchId: string;
  seed: number;
  speed: number;
  events: number;
}

export const PERSONAS = [
  { id: 'analyst', label: 'Analyst' },
  { id: 'casual', label: 'Casual' },
  { id: 'club_fan', label: 'Club fan' },
  { id: 'player_focus', label: 'Player focus' },
] as const;

export const LANGUAGES = [
  { id: 'en', label: 'English' },
  { id: 'af', label: 'Afrikaans' },
  { id: 'zu', label: 'isiZulu' },
  { id: 'es', label: 'Español' },
] as const;
