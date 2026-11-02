import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Adds the bearer token to API calls. On a 401, refreshes once and retries with the new token.
 * /api/auth/* is left alone: it authenticates with the cookie, and refreshing on its own 401 would loop.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (request.url.startsWith('/api/auth/')) {
    return next(request);
  }

  const auth = inject(AuthService);

  return next(withBearer(request, auth.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      return from(auth.refresh()).pipe(
        switchMap((refreshed) =>
          refreshed ? next(withBearer(request, auth.accessToken())) : throwError(() => error),
        ),
      );
    }),
  );
};

function withBearer(request: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;
}
