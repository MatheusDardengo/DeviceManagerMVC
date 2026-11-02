import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { authInterceptor } from './auth-interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let client: HttpClient;
  let auth: AuthService;
  let http: HttpTestingController;

  const tokenResponse = (accessToken: string) => ({ accessToken, expiresAtUtc: '2026-01-10T09:15:00Z' });
  const unauthorized = { status: 401, statusText: 'Unauthorized' };

  // Lets pending promises (refresh) resolve before we look for the retried request.
  const settle = () => new Promise((resolve) => setTimeout(resolve));

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    client = TestBed.inject(HttpClient);
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function givenLoggedIn(accessToken = 'jwt'): Promise<void> {
    const login = auth.login('user@example.com', 'Passw0rd!');
    http.expectOne('/api/auth/login').flush(tokenResponse(accessToken));
    await login;
  }

  it('adds the bearer token when logged in', async () => {
    await givenLoggedIn('jwt');

    const call = firstValueFrom(client.get('/api/devices'));
    const request = http.expectOne('/api/devices');
    request.flush([]);
    await call;

    expect(request.request.headers.get('Authorization')).toBe('Bearer jwt');
  });

  it('sends no Authorization header when logged out', async () => {
    const call = firstValueFrom(client.get('/api/devices'));
    const request = http.expectOne('/api/devices');
    request.flush([]);
    await call;

    expect(request.request.headers.has('Authorization')).toBe(false);
  });

  it('never sends the bearer token to auth endpoints', async () => {
    await givenLoggedIn('jwt');

    const call = firstValueFrom(client.post('/api/auth/logout', null));
    const request = http.expectOne('/api/auth/logout');
    request.flush(null);
    await call;

    expect(request.request.headers.has('Authorization')).toBe(false);
  });

  it('on 401 refreshes and retries once with the new token', async () => {
    await givenLoggedIn('expired-jwt');

    const call = firstValueFrom(client.get<string[]>('/api/devices'));
    http.expectOne('/api/devices').flush({ title: 'Unauthorized' }, unauthorized);
    http.expectOne('/api/auth/refresh').flush(tokenResponse('new-jwt'));
    await settle();
    const retry = http.expectOne('/api/devices');
    retry.flush(['device']);

    expect(await call).toEqual(['device']);
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new-jwt');
  });

  it('on 401 with a failed refresh returns the original 401 without retrying', async () => {
    await givenLoggedIn();

    const call = firstValueFrom(client.get('/api/devices'));
    http.expectOne('/api/devices').flush({ title: 'Unauthorized' }, unauthorized);
    http.expectOne('/api/auth/refresh').flush({ title: 'Unauthorized' }, unauthorized);

    const error = await call.catch((e: unknown) => e);
    expect(error).toBeInstanceOf(HttpErrorResponse);
    expect((error as HttpErrorResponse).status).toBe(401);
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('does not refresh on errors other than 401', async () => {
    await givenLoggedIn();

    const call = firstValueFrom(client.get('/api/devices/999'));
    http.expectOne('/api/devices/999').flush({ title: 'Not Found' }, { status: 404, statusText: 'Not Found' });

    await expect(call).rejects.toBeInstanceOf(HttpErrorResponse);
  });

  it('does not refresh when an auth endpoint itself returns 401', async () => {
    const call = firstValueFrom(client.post('/api/auth/login', {}));
    http.expectOne('/api/auth/login').flush({ title: 'Unauthorized' }, unauthorized);

    await expect(call).rejects.toBeInstanceOf(HttpErrorResponse);
  });
});
