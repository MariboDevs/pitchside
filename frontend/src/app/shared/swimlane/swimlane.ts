import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Narrative } from '../../core/contracts';

/** One box in the agent trace. */
interface Stage {
  name: string;
  detail: string;
  retried: boolean;
}

/**
 * The agent trace for one moment as a horizontal swimlane: explainer, fact-checker, fallback when it ran, persona
 * writer and translator. A stage that retried is red. Built from the narrative's provenance.
 */
@Component({
  selector: 'app-swimlane',
  templateUrl: './swimlane.html',
  styleUrl: './swimlane.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Swimlane {
  readonly narrative = input.required<Narrative>();

  protected readonly stages = computed<Stage[]>(() => {
    const p = this.narrative().provenance;
    const retried = p.attempts > 1;
    const stages: Stage[] = [
      { name: 'Explainer', detail: `${p.attempts} attempt${p.attempts === 1 ? '' : 's'}`, retried },
      { name: 'Fact-check', detail: p.fallback ? 'rejected' : 'passed', retried },
    ];
    if (p.fallback) {
      stages.push({ name: 'Template', detail: 'fallback', retried: false });
    }

    stages.push(
      { name: 'Persona', detail: 'base', retried: false },
      { name: 'Translate', detail: 'en', retried: false },
    );
    return stages;
  });
}
