import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { authGuard } from './auth-guard';
import { AuthService } from './auth.service';

describe('authGuard', () => {
  let loggedIn: boolean;
  const refresh = vi.fn<() => Promise<boolean>>();

  beforeEach(() => {
    loggedIn = false;
    refresh.mockReset();

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { isAuthenticated: () => loggedIn, refresh } },
      ],
    });
  });

  const runGuard = () =>
    TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));

  it('allows navigation when already logged in, without refreshing', async () => {
    loggedIn = true;

    expect(await runGuard()).toBe(true);
    expect(refresh).not.toHaveBeenCalled();
  });

  it('restores the session with a refresh after a page reload', async () => {
    refresh.mockResolvedValue(true);

    expect(await runGuard()).toBe(true);
  });

  it('redirects to /login when there is no session', async () => {
    refresh.mockResolvedValue(false);

    const result = await runGuard();

    expect(result).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/login');
  });
});
