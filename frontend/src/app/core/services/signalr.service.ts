import { Injectable } from '@angular/core';
import { BehaviorSubject, Subject, distinctUntilChanged } from 'rxjs';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  HttpTransportType,
} from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { OrderProgressService } from './order-progress.service';
import { OrderStatusNotification, OrderWorkflowState } from '../models';
import { ToastService } from './toast.service';

type CorrelationIdDto = { value: string } | string;
type OrderWorkflowStatusDto = number | 'Accepted' | 'Processing' | 'Completed';

interface OrderStatusNotificationDto {
  correlationId: CorrelationIdDto;
  status: OrderWorkflowStatusDto;
  orderId: number | null;
}

interface OrderWorkflowStateDto {
  status: OrderWorkflowStatusDto;
  orderId: number | null;
}

function normalizeCorrelationId(c: CorrelationIdDto): string {
  return typeof c === 'string' ? c : (c?.value ?? '');
}

function normalizeStatus(s: OrderWorkflowStatusDto): 'Accepted' | 'Processing' | 'Completed' {
  if (typeof s === 'string') return s;
  switch (s) {
    case 0: return 'Accepted';
    case 1: return 'Processing';
    case 2: return 'Completed';
    default: return 'Processing';
  }
}

@Injectable({ providedIn: 'root' })
export class SignalRService {
  private connection: HubConnection | null = null;

  private readonly _state$ = new BehaviorSubject<'disconnected' | 'connecting' | 'connected'>('disconnected');
  readonly state$ = this._state$.asObservable();

  private readonly _notifications$ = new Subject<OrderStatusNotification>();
  readonly notifications$ = this._notifications$.asObservable();

  constructor(
    private auth: AuthService,
    private progress: OrderProgressService,
    private toasts: ToastService
  ) {
    // If user changes while connected, force a reconnect.
    this.auth.userId$
      .pipe(distinctUntilChanged())
      .subscribe(() => {
        // We cannot seamlessly “switch” user on an existing SignalR connection.
        // Disconnect and let pages call ensureConnected() again.
        void this.disconnect();
      });
  }

  async ensureConnected(): Promise<void> {
    if (!this.auth.isLoggedIn()) {
      throw new Error('User is not authenticated');
    }
    if (this.connection?.state === HubConnectionState.Connected) return;
    if (this._state$.value === 'connecting') return;

    this._state$.next('connecting');

    const base = environment.signalRBaseUrl.replace(/\/$/, '');
    const hubUrl = `${base}/hubs/order-status`;

    // NOTE about DevAuthenticationHandler in backend:
    // - It currently checks only Authorization header.
    // - Browser WebSockets cannot send Authorization headers; SignalR uses ?access_token=... for WS.
    // Best practice is backend: accept token from query `access_token` too.
    //
    // If you haven't patched backend yet, you can force LongPolling below.
    const forceLongPolling = environment.useMocks; // dev-friendly. set false when backend supports WS token.

    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => this.auth.getAccessToken(),
        transport: forceLongPolling ? HttpTransportType.LongPolling : HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.connection.on('Notification', (dto: OrderStatusNotificationDto) => {
      const n: OrderStatusNotification = {
        correlationId: normalizeCorrelationId(dto.correlationId),
        status: normalizeStatus(dto.status),
        orderId: dto.orderId ?? null,
      };

      this._notifications$.next(n);
      this.progress.applyNotification(n);

      const title = n.status === 'Completed' ? 'Order completed' : 'Order update';
      this.toasts.push(n.status === 'Completed' ? 'success' : 'info', title, `CorrelationId: ${n.correlationId}`);
    });

    this.connection.onreconnected(async () => {
      this._state$.next('connected');

      // Re-register active correlation (late-join / reconnect)
      const active = this.progress.snapshot;
      if (active) {
        await this.safeRegister(active.correlationId);
        // pull current status too
        const state = await this.safeGetCurrentStatus(active.correlationId);
        if (state) this.progress.setStatus(state.status, state.orderId);
      }
    });

    this.connection.onclose(() => {
      this._state$.next('disconnected');
    });

    await this.connection.start();
    this._state$.next('connected');

    // If there is an active order, register it immediately so that server routes notifications
    const active = this.progress.snapshot;
    if (active) {
      await this.safeRegister(active.correlationId);
      const state = await this.safeGetCurrentStatus(active.correlationId);
      if (state) this.progress.setStatus(state.status, state.orderId);
    }
  }

  async ping(): Promise<void> {
    if (!this.connection) throw new Error('SignalR not initialized');
    await this.connection.invoke('Ping');
  }

  async registerOrder(correlationId: string): Promise<void> {
    if (!this.connection) throw new Error('SignalR not initialized');
    await this.connection.invoke('RegisterOrder', correlationId);
  }

  async getCurrentStatus(correlationId: string): Promise<OrderWorkflowState | null> {
    if (!this.connection) throw new Error('SignalR not initialized');
    const dto = await this.connection.invoke<OrderWorkflowStateDto | null>('GetCurrentStatus', correlationId);
    if (!dto) return null;
    return { status: normalizeStatus(dto.status), orderId: dto.orderId ?? null };
  }

  private async safeRegister(correlationId: string): Promise<void> {
    try { await this.registerOrder(correlationId); }
    catch (e) { console.warn('RegisterOrder failed:', e); }
  }

  private async safeGetCurrentStatus(correlationId: string): Promise<OrderWorkflowState | null> {
    try { return await this.getCurrentStatus(correlationId); }
    catch (e) { console.warn('GetCurrentStatus failed:', e); return null; }
  }

  async disconnect(): Promise<void> {
    if (!this.connection) {
      this._state$.next('disconnected');
      return;
    }
    try {
      await this.connection.stop();
    } finally {
      this.connection = null;
      this._state$.next('disconnected');
    }
  }
}
