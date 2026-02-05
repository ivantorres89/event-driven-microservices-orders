import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, delay, EMPTY, expand, firstValueFrom, map, Observable, of, reduce, tap } from 'rxjs';
import { ActiveOrder, CartLine, Order } from '../models';
import { StorageService } from './storage.service';
import { RuntimeConfigService } from './runtime-config.service';

const ORDERS_KEY = 'contoso.orders.v1';

type PageEnvelope<T> = {
  offset: number;
  size: number;
  total: number;
  items: T[];
};

export type OrderItemDto = {
  productId: string;
  productName: string;
  imageUrl?: string;
  unitPrice: number;
  quantity: number;
};

export type OrderDto = {
  id: number;
  correlationId: string;
  createdAt: string;
  items: OrderItemDto[];
};

type CreateOrderRequest = {
  items: Array<{ productId: string; quantity: number }>;
};

function uuidv4(): string {
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

  constructor(
    private storage: StorageService,
    private http: HttpClient,
    private cfg: RuntimeConfigService
  ) {
    const existing = this.storage.getJson<Order[]>(ORDERS_KEY) ?? [];
    this._orders$.next(existing);
  }

  get snapshot(): Order[] {
    return this._orders$.value;
  }

  /** Option A: carga todas las páginas de GET /api/orders?offset=&size= */
  refreshAll(pageSize = 50): Observable<Order[]> {
    if (this.cfg.useMocks) {
      const existing = this.storage.getJson<Order[]>(ORDERS_KEY) ?? [];
      this._orders$.next(existing);
      return of(existing).pipe(delay(150));
    }

    return this.fetchAllOrders(pageSize).pipe(
      tap(list => {
        this._orders$.next(list);
        this.storage.setJson(ORDERS_KEY, list);
      })
    );
  }

    /** GET /api/orders/{id} (read-only order detail) */
  getOrderById(orderId: number): Observable<OrderDto> {
    if (this.cfg.useMocks) {
      // In mock mode, try to build a detail from local storage snapshot.
      const hit = this._orders$.value.find(o => o.id === orderId);
      if (!hit) return of({ id: orderId, correlationId: 'mock', createdAt: new Date().toISOString(), items: [] } as OrderDto).pipe(delay(150));

      return of({
        id: hit.id,
        correlationId: hit.correlationId,
        createdAt: hit.createdAtUtc,
        items: hit.lines.map(l => ({
          productId: l.productId,
          productName: l.productName,
          unitPrice: l.unitPrice,
          quantity: l.quantity,
          imageUrl: undefined
        }))
      } as OrderDto).pipe(delay(150));
    }

    const base = this.cfg.orderAcceptApiBaseUrl.replace(/\/$/, '');
    const url = `${base}/api/orders/${orderId}`;
    return this.http.get<OrderDto>(url);
  }

/** POST /api/orders (checkout) */
  createOrder(lines: CartLine[]): Observable<Order> {
    if (this.cfg.useMocks) {
      const correlationId = uuidv4();
      const mock: Order = {
        id: Math.floor(100000 + Math.random() * 900000),
        correlationId,
        createdAtUtc: new Date().toISOString(),
        lines: lines.map(l => ({
          productId: l.product.id,
          productName: l.product.name,
          unitPrice: l.product.price,
          quantity: l.quantity
        }))
      };

      this.upsertLocal(mock);
      return of(mock).pipe(delay(250));
    }

    const base = this.cfg.orderAcceptApiBaseUrl.replace(/\/$/, '');
    const url = `${base}/api/orders`;

    const body: CreateOrderRequest = {
      items: lines.map(l => ({
        productId: l.product.id,
        quantity: l.quantity
      }))
    };

    return this.http.post<OrderDto>(url, body).pipe(
      map(dtoToOrder),
      tap(order => this.upsertLocal(order))
    );
  }

  addFromActive(active: ActiveOrder): Order {
    if (!active.orderId) throw new Error('Active order has no orderId yet');

    const order: Order = {
      id: active.orderId,
      correlationId: active.correlationId,
      createdAtUtc: active.createdAtUtc,
      lines: active.lines
    };

    this.upsertLocal(order);
    return order;
  }

  /** DELETE /api/orders/{id} */
  async deleteOrder(orderId: number): Promise<void> {
    if (!orderId) return;

    if (!this.cfg.useMocks) {
      const base = this.cfg.orderAcceptApiBaseUrl.replace(/\/$/, '');
      const url = `${base}/api/orders/${orderId}`;

      try {
        await firstValueFrom(this.http.delete<void>(url));
      } catch (e) {
        console.warn('DELETE /api/orders failed (still removing locally):', e);
      }
    }

    const orders = this._orders$.value.filter(o => o.id !== orderId);
    this._orders$.next(orders);
    this.storage.setJson(ORDERS_KEY, orders);
  }

  private fetchOrdersPage(offset: number, size: number): Observable<PageEnvelope<OrderDto>> {
    const base = this.cfg.orderAcceptApiBaseUrl.replace(/\/$/, '');
    const url = `${base}/api/orders?offset=${offset}&size=${size}`;
    return this.http.get<PageEnvelope<OrderDto>>(url);
  }

  private fetchAllOrders(pageSize: number): Observable<Order[]> {
    return this.fetchOrdersPage(0, pageSize).pipe(
      expand((page) => {
        const nextOffset = (page.offset ?? 0) + (page.size ?? pageSize);
        if (nextOffset >= (page.total ?? 0)) return EMPTY;
        return this.fetchOrdersPage(nextOffset, pageSize);
      }),
      map(page => (page.items ?? []).map(dtoToOrder)),
      reduce((acc, batch) => acc.concat(batch), [] as Order[])
    );
  }

  private upsertLocal(order: Order): void {
    const orders = [order, ...this._orders$.value.filter(o => o.id !== order.id)];
    this._orders$.next(orders);
    this.storage.setJson(ORDERS_KEY, orders);
  }
}

function dtoToOrder(dto: OrderDto): Order {
  return {
    id: dto.id,
    correlationId: dto.correlationId,
    createdAtUtc: dto.createdAt,
    lines: (dto.items ?? []).map(i => ({
      productId: i.productId,
      productName: i.productName,
      unitPrice: i.unitPrice,
      quantity: i.quantity
    }))
  };
}
