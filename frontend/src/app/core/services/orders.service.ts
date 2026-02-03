import { Injectable } from '@angular/core';
import { BehaviorSubject, delay, Observable, of } from 'rxjs';
import { ActiveOrder, CartLine, Order } from '../models';
import { StorageService } from './storage.service';
import { environment } from '../../../environments/environment';

const ORDERS_KEY = 'contoso.orders.v1';

function uuidv4(): string {
  // Good enough for demo
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = Math.random() * 16 | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

@Injectable({ providedIn: 'root' })
export class OrdersService {
  private readonly _orders$ = new BehaviorSubject<Order[]>([]);
  readonly orders$ = this._orders$.asObservable();

  constructor(private storage: StorageService) {
    const existing = this.storage.getJson<Order[]>(ORDERS_KEY) ?? [];
    this._orders$.next(existing);
  }

  get snapshot(): Order[] {
    return this._orders$.value;
  }

  addFromActive(active: ActiveOrder): Order {
    if (!active.orderId) throw new Error('Active order has no orderId yet');

    const order: Order = {
      id: active.orderId,
      correlationId: active.correlationId,
      createdAtUtc: active.createdAtUtc,
      lines: active.lines
    };

    const orders = [order, ...this._orders$.value.filter(o => o.id !== order.id)];
    this._orders$.next(orders);
    this.storage.setJson(ORDERS_KEY, orders);
    return order;
  }

  // Mock submit to order-accept: returns correlationId (real backend would do this)
  submitOrderMock(lines: CartLine[]): Observable<{ correlationId: string }> {
    const correlationId = uuidv4();
    return of({ correlationId }).pipe(delay(250));
  }

  // Optional: real API (you can wire it later)
  // submitOrderReal(lines: CartLine[]): Observable<{ correlationId: string }> { ... }

  async deleteOrder(orderId: number): Promise<void> {
    // Requirement: call order-accept DELETE /api/orders/{id}
    const url = `${environment.orderAcceptApiBaseUrl.replace(/\/$/, '')}/api/orders/${orderId}`;
    const res = await fetch(url, { method: 'DELETE' });

    // Even if backend is down in demo, we remove locally if 404/500 is acceptable.
    // If you prefer strictness, check res.ok only.
    const orders = this._orders$.value.filter(o => o.id !== orderId);
    this._orders$.next(orders);
    this.storage.setJson(ORDERS_KEY, orders);

    if (!res.ok) {
      console.warn('DELETE failed (demo will still remove locally):', res.status, await res.text().catch(() => ''));
    }
  }
}
