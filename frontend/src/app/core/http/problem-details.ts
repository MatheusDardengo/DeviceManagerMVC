import { HttpErrorResponse } from '@angular/common/http';

export function problemMessage(error: unknown, fallback: string): string {
  if (error instanceof HttpErrorResponse && error.status >= 400 && error.status < 500) {
    const problem = error.error as { detail?: unknown; title?: unknown } | null;
    for (const text of [problem?.detail, problem?.title]) {
      if (typeof text === 'string' && text.trim()) {
        return text;
      }
    }
  }

  return fallback;
}
