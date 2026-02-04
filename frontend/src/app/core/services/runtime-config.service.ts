import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

export type RuntimeConfig = {
  signalRBaseUrl?: string;
  orderAcceptApiBaseUrl?: string;
  defaultUserId?: string;
  signalRForceLongPolling?: boolean;
  useMocks?: boolean;
};

@Injectable({ providedIn: 'root' })
export class RuntimeConfigService {
  private cfg: RuntimeConfig | null = null;

  /**
   * Loads runtime config from /assets/runtime-config.json.
   * In Docker Compose, this file is generated at container startup from infra/local/.env.
   */
  async load(): Promise<void> {
    try {
      const res = await fetch('/assets/runtime-config.json', { cache: 'no-store' });
      if (!res.ok) {
        console.warn('[runtime-config] not found, using environment.ts defaults');
        this.cfg = {};
        return;
      }
      this.cfg = await res.json();
      console.log('[runtime-config] loaded');
    } catch (e) {
      console.warn('[runtime-config] failed to load, using environment.ts defaults', e);
      this.cfg = {};
    }
  }

  get signalRBaseUrl(): string {
    return (this.cfg?.signalRBaseUrl ?? environment.signalRBaseUrl).trim();
  }

  get orderAcceptApiBaseUrl(): string {
    return (this.cfg?.orderAcceptApiBaseUrl ?? environment.orderAcceptApiBaseUrl).trim();
  }

  get defaultUserId(): string {
    return (this.cfg?.defaultUserId ?? environment.defaultUserId ?? '').trim();
  }

  get signalRForceLongPolling(): boolean {
    return (this.cfg?.signalRForceLongPolling ?? (environment as any).signalRForceLongPolling ?? false) === true;
  }

  get useMocks(): boolean {
    return (this.cfg?.useMocks ?? environment.useMocks) === true;
  }
}
