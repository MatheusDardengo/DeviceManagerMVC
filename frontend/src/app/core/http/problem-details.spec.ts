import { HttpErrorResponse } from '@angular/common/http';
import { problemMessage } from './problem-details';

describe('problemMessage', () => {
  const fallback = 'Something went wrong.';

  it('uses the ProblemDetails detail of a 4xx response', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { title: 'Business rule violated', detail: 'A device in use cannot be deleted.' },
    });

    expect(problemMessage(error, fallback)).toBe('A device in use cannot be deleted.');
  });

  it('falls back to the title when there is no detail', () => {
    const error = new HttpErrorResponse({ status: 400, error: { title: 'One or more validation errors occurred.' } });

    expect(problemMessage(error, fallback)).toBe('One or more validation errors occurred.');
  });

  it('never shows the body of a 5xx response', () => {
    const error = new HttpErrorResponse({ status: 500, error: { title: 'Internal detail' } });

    expect(problemMessage(error, fallback)).toBe(fallback);
  });

  it('uses the fallback for errors that are not HTTP errors', () => {
    expect(problemMessage(new Error('network'), fallback)).toBe(fallback);
  });
});
