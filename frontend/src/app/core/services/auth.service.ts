import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { StorageService } from './storage.service';

type AuthSession = {
  userId: string;
  token: string;
  expiresAtUtc: string; // ISO string
};

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly key = 'contoso.auth.v1';

  private readonly _session$ = new BehaviorSubject<AuthSession | null>(null);
  readonly session$ = this._session$.asObservable();

  constructor(
    private storage: StorageService,
    private http: HttpClient
  ) {
    this._session$.next(this.loadInitialSession());
  }

  getUserId(): string {
    return this._session$.value?.userId ?? '';
  }

  /** Used by SignalR accessTokenFactory and REST Authorization header. */
  getAccessToken(): string {
    return this._session$.value?.token ?? '';
  }

  isLoggedIn(): boolean {
    const s = this._session$.value;
    if (!s) return false;
    if (!s.token || !s.userId) return false;
    const exp = Date.parse(s.expiresAtUtc);
    if (!Number.isFinite(exp)) return false;
    return Date.now() < exp;
  }

  /**
   * Development login:
   * Calls order-notification /dev/token to obtain a signed JWT for the given userId.
   * The backend is responsible for issuing the token (NOT the SPA).
   */
  async loginDev(userId: string): Promise<void> {
    const cleaned = (userId ?? '').trim();
    if (!cleaned) throw new Error('userId is required');

    const base = environment.signalRBaseUrl.replace(/\/$/, '');
    const url = `${base}/dev/token`;

    const res = await firstValueFrom(
      this.http.post<{ userId: string; token: string; expiresAtUtc: string }>(url, { userId: cleaned })
    );

    const session: AuthSession = {
      userId: res.userId,
      token: res.token,
      expiresAtUtc: res.expiresAtUtc
    };

    this.storage.setJson(this.key, session);
    this._session$.next(session);
  }

  logout(): void {
    this.storage.remove(this.key);
    this._session$.next(null);
  }

  /** Convenience for Login UI: last userId if present, otherwise environment.defaultUserId (optional). */
  getSuggestedUserId(): string {
    const stored = this.storage.getJson<AuthSession>(this.key);
    const v = (stored?.userId ?? '').trim();
    if (v) return v;
    return (environment.defaultUserId ?? '').trim();
  }

  private loadInitialSession(): AuthSession | null {
    const stored = this.storage.getJson<AuthSession>(this.key);
    if (!stored) return null;

    const userId = (stored.userId ?? '').trim();
    const token = (stored.token ?? '').trim();
    const expiresAtUtc = (stored.expiresAtUtc ?? '').trim();

    if (!userId || !token || !expiresAtUtc) return null;

    const exp = Date.parse(expiresAtUtc);
    if (!Number.isFinite(exp)) return null;
    if (Date.now() >= exp) return null;

    return { userId, token, expiresAtUtc };
  }
}
