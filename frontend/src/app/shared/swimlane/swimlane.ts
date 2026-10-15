import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { AuditRecord, Narrative } from '../../core/contracts';

/** One box in the agent trace. */
interface Stage {
  name: string;
  detail: string;
  retried: boolean;
  /** The stage's model time from the audit records, when they are loaded. */
  timing: string | null;
}

/** Which audit stages make up each box: the prompts that ran for it. */
const AUDIT_STAGES: Record<string, (stage: string) => boolean> = {
  Explainer: (s) => s === 'explainer',
  'Fact-check': (s) => s === 'factcheck',
  Persona: (s) => s.startsWith('persona-'),
  Translate: (s) => s === 'translator',
};

/**
 * The agent trace for one moment as a horizontal swimlane: explainer, fact-checker, fallback when it ran, persona
 * writer and translator. A stage that retried is red. Built from the narrative's provenance; with the moment's audit
 * records (WP-07), each box also shows its model calls' total latency and tokens.
 */
@Component({
  selector: 'app-swimlane',
  templateUrl: './swimlane.html',
  styleUrl: './swimlane.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Swimlane {
  readonly narrative = input.required<Narrative>();
  readonly calls = input<AuditRecord[]>([]);

  protected readonly stages = computed<Stage[]>(() => {
    const p = this.narrative().provenance;
    const retried = p.attempts > 1;
    const stages: Stage[] = [
      {
        name: 'Explainer',
        detail: `${p.attempts} attempt${p.attempts === 1 ? '' : 's'}`,
        retried,
        timing: null,
      },
      { name: 'Fact-check', detail: p.fallback ? 'rejected' : 'passed', retried, timing: null },
    ];
    if (p.fallback) {
      stages.push({ name: 'Template', detail: 'fallback', retried: false, timing: null });
    }

    stages.push(
      { name: 'Persona', detail: 'base', retried: false, timing: null },
      { name: 'Translate', detail: 'en', retried: false, timing: null },
    );
    return stages.map((stage) => ({ ...stage, timing: this.timing(stage.name) }));
  });

  private timing(name: string): string | null {
    const matches = AUDIT_STAGES[name];
    const calls = matches ? this.calls().filter((c) => matches(c.stage)) : [];
    if (calls.length === 0) {
      return null;
    }

    const ms = calls.reduce((sum, c) => sum + c.latencyMs, 0);
    const tokens = calls.reduce((sum, c) => sum + c.inputTokens + c.outputTokens, 0);
    const time = ms >= 1000 ? `${(ms / 1000).toFixed(1)} s` : `${ms} ms`;
    return `${calls.length} call${calls.length === 1 ? '' : 's'}, ${time}, ${tokens} tokens`;
  }
}
