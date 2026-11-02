import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  const tokenResponse = (accessToken: string) => ({ accessToken, expiresAtUtc: '2026-01-10T09:15:00Z' });
  const unauthorized = { status: 401, statusText: 'Unauthorized' };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function givenLoggedIn(accessToken = 'jwt'): Promise<void> {
    const login = service.login('user@example.com', 'Passw0rd!');
    http.expectOne('/api/auth/login').flush(tokenResponse(accessToken));
    await login;
  }

  it('starts logged out', () => {
    expect(service.accessToken()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
  });

  // ---------- login ----------

  it('login posts the credentials and keeps the access token in memory', async () => {
    const login = service.login('user@example.com', 'Passw0rd!');

    const request = http.expectOne('/api/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'user@example.com', password: 'Passw0rd!' });
    request.flush(tokenResponse('jwt'));
    await login;

    expect(service.accessToken()).toBe('jwt');
    expect(service.isAuthenticated()).toBe(true);
  });

  it('login with invalid credentials rejects and stays logged out', async () => {
    const login = service.login('user@example.com', 'wrong');

    http.expectOne('/api/auth/login').flush({ title: 'Unauthorized' }, unauthorized);

    await expect(login).rejects.toBeInstanceOf(HttpErrorResponse);
    expect(service.isAuthenticated()).toBe(false);
  });

  // ---------- refresh ----------

  it('refresh posts to the refresh endpoint and replaces the access token', async () => {
    await givenLoggedIn('old-jwt');

    const refresh = service.refresh();
    const request = http.expectOne('/api/auth/refresh');
    expect(request.request.method).toBe('POST');
    request.flush(tokenResponse('new-jwt'));

    expect(await refresh).toBe(true);
    expect(service.accessToken()).toBe('new-jwt');
  });

  it('refresh that fails returns false and logs out', async () => {
    await givenLoggedIn();

    const refresh = service.refresh();
    http.expectOne('/api/auth/refresh').flush({ title: 'Unauthorized' }, unauthorized);

    expect(await refresh).toBe(false);
    expect(service.isAuthenticated()).toBe(false);
  });

  // ---------- logout ----------

  it('logout posts to the logout endpoint and clears the access token', async () => {
    await givenLoggedIn();

    const logout = service.logout();
    const request = http.expectOne('/api/auth/logout');
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await logout;

    expect(service.isAuthenticated()).toBe(false);
  });
});