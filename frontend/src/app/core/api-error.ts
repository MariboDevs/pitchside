import { HttpErrorResponse } from '@angular/common/http';

/** A message fit to show for a failed call: the API's problem-details title or detail, never a raw response. */
export function toApiError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const problem = error.error as { title?: string; detail?: string } | null;
    return problem?.detail ?? problem?.title ?? `The request failed (${error.status}).`;
  }

  return 'Something went wrong. Try again.';
}
