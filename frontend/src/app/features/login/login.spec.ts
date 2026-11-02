import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { Login } from './login';

describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  let router: Router;
  const login = vi.fn<(email: string, password: string) => Promise<void>>();

  beforeEach(async () => {
    login.mockReset();

    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideRouter([]), { provide: AuthService, useValue: { login } }],
    }).compileComponents();

    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);

    fixture = TestBed.createComponent(Login);
    await fixture.whenStable();
  });

  const page = () => fixture.nativeElement as HTMLElement;

  function type(selector: string, value: string): void {
    const input = page().querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  async function submit(): Promise<void> {
    page().querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    // Let the async submit() finish, then let Angular re-render.
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  }

  it('signs in with the typed credentials and goes to /devices', async () => {
    login.mockResolvedValue();
    type('input[type="email"]', 'user@example.com');
    type('input[type="password"]', 'Passw0rd!');

    await submit();

    expect(login).toHaveBeenCalledWith('user@example.com', 'Passw0rd!');
    expect(router.navigate).toHaveBeenCalledWith(['/devices']);
  });

  it('does not call the API when the form is invalid and shows the field errors', async () => {
    await submit();

    expect(login).not.toHaveBeenCalled();
    expect(page().textContent).toContain('Email is required.');
    expect(page().textContent).toContain('Password is required.');
  });

  it('shows "Invalid email or password." on a 401 and stays on the page', async () => {
    login.mockRejectedValue(new HttpErrorResponse({ status: 401 }));
    type('input[type="email"]', 'user@example.com');
    type('input[type="password"]', 'wrong-password');

    await submit();

    expect(page().querySelector('[role="alert"]')?.textContent).toContain('Invalid email or password.');
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('shows a generic message when the server fails', async () => {
    login.mockRejectedValue(new HttpErrorResponse({ status: 500 }));
    type('input[type="email"]', 'user@example.com');
    type('input[type="password"]', 'Passw0rd!');

    await submit();

    expect(page().querySelector('[role="alert"]')?.textContent).toContain('Could not sign in. Please try again.');
  });
});
