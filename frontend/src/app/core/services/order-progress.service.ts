import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { ActiveOrder, CartLine, OrderStatusNotification, OrderWorkflowStatus } from '../models';
import { StorageService } from './storage.service';

const ACTIVE_ORDER_KEY = 'contoso.activeOrder.v1';

@Injectable({ providedIn: 'root' })
export class OrderProgressService {
  private readonly _activeOrder$ = new BehaviorSubject<ActiveOrder | null>(null);
  readonly activeOrder$ = this._activeOrder$.asObservable();

  constructor(private storage: StorageService) {
    const existing = this.storage.getJson<ActiveOrder>(ACTIVE_ORDER_KEY);
    if (existing) this._activeOrder$.next(existing);
  }

  get snapshot(): ActiveOrder | null {
    return this._activeOrder$.value;
  }

  startNew(correlationId: string, lines: CartLine[]): ActiveOrder {
    const now = new Date().toISOString();
    const order: ActiveOrder = {
      correlationId,
      createdAtUtc: now,
      status: 'Accepted',
      orderId: null,
      lines: lines.map(l => ({
        productId: l.product.id,
        productName: l.product.name,
        unitPrice: l.product.price,
        quantity: l.quantity,
      }))
    };

    this._activeOrder$.next(order);
    this.storage.setJson(ACTIVE_ORDER_KEY, order);
    return order;
  }

  setStatus(status: OrderWorkflowStatus, orderId: number | null): void {
    const current = this._activeOrder$.value;
    if (!current) return;

    const updated: ActiveOrder = { ...current, status, orderId };
    this._activeOrder$.next(updated);
    this.storage.setJson(ACTIVE_ORDER_KEY, updated);
  }

  applyNotification(n: OrderStatusNotification): void {
    const current = this._activeOrder$.value;
    if (!current) return;
    if (current.correlationId !== n.correlationId) return;

    this.setStatus(n.status, n.orderId);
  }

  clear(): void {
    this._activeOrder$.next(null);
    this.storage.remove(ACTIVE_ORDER_KEY);
  }
}
