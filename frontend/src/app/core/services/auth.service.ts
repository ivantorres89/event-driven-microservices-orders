import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { StorageService } from './storage.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  // In dev: token value == userId (backend DevAuthenticationHandler)
  // Persisted so user can close the tab and the session can recover.
  private readonly key = 'contoso.userId';

  private readonly _userId$ = new BehaviorSubject<string>('');
  readonly userId$ = this._userId$.asObservable();

  constructor(private storage: StorageService) {
    this._userId$.next(this.loadInitialUserId());
  }

  /** Returns current user id (may fall back to environment.demoUserId if nothing set). */
  getUserId(): string {
    return this._userId$.value;
  }

  /** SignalR uses this as the access token; in dev it's equal to the userId. */
  getAccessToken(): string {
    return this._userId$.value;
  }

  isLoggedIn(): boolean {
    return (this._userId$.value ?? '').trim().length > 0;
  }

  /** “Login” for the demo: userId becomes the bearer token and the SignalR user identifier. */
  login(userId: string): void {
    const cleaned = (userId ?? '').trim();
    this.storage.setJson(this.key, { userId: cleaned, loggedOut: false });
    this._userId$.next(cleaned);
  }

  logout(): void {
    // Persist the explicit logout so we don't auto-fallback to environment.demoUserId after a refresh.
    this.storage.setJson(this.key, { userId: '', loggedOut: true });
    this._userId$.next('');
  }

  private loadInitialUserId(): string {
    const stored = this.storage.getJson<{ userId: string; loggedOut?: boolean }>(this.key);
    if (stored?.loggedOut) return '';
    const v = (stored?.userId ?? '').trim();
    return v.length > 0 ? v : (environment.demoUserId ?? '');
  }
}
