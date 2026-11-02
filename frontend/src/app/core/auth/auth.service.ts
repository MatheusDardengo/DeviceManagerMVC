import { HttpClient } from '@angular/common/http';
import { computed, inject, Service, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface AccessTokenResponse {
  accessToken: string;
  expiresAtUtc: string;
}

@Service()
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly token = signal<string | null>(null);

  readonly accessToken = this.token.asReadonly();
  readonly isAuthenticated = computed(() => this.token() !== null);

  /** Rejects with the HTTP error (401 for invalid credentials); the caller shows the message. */
  async login(email: string, password: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<AccessTokenResponse>('/api/auth/login', { email, password }),
    );
    this.token.set(response.accessToken);
  }

  /** Uses the refresh cookie the browser sends automatically. Returns false when the session is gone. */
  async refresh(): Promise<boolean> {
    try {
      const response = await firstValueFrom(
        this.http.post<AccessTokenResponse>('/api/auth/refresh', null),
      );
      this.token.set(response.accessToken);
      return true;
    } catch {
      this.token.set(null);
      return false;
    }
  }

  /** Always ends logged out locally, even if the server call fails. */
  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post<void>('/api/auth/logout', null));
    } finally {
      this.token.set(null);
    }
  }
}