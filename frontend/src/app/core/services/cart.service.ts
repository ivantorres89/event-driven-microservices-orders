import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { CartLine, Product } from '../models';
import { StorageService } from './storage.service';

const CART_KEY = 'contoso.cart.v1';

type CartEnvelope = {
  v: 1;
  origin: string;
  updatedAt: string;
  lines: CartLine[];
};

@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly _lines$ = new BehaviorSubject<CartLine[]>([]);
  readonly lines$ = this._lines$.asObservable();

  private readonly _overlayOpen$ = new BehaviorSubject<boolean>(false);
  readonly overlayOpen$ = this._overlayOpen$.asObservable();

  private readonly tabId: string;
  private lastOrigin: string | null = null;

  constructor(private storage: StorageService) {
    this.tabId = this.getOrCreateTabId();

    // Load initial cart snapshot
    const savedRaw = this.storage.getJson<CartEnvelope | CartLine[]>(CART_KEY);
    const savedLines = this.extractLines(savedRaw);
    if (savedLines) {
      this._lines$.next(savedLines.lines);
      this.lastOrigin = savedLines.origin;
    }

    // Sync cart between tabs
    window.addEventListener('storage', (e) => {
      if (e.key !== CART_KEY) return;

      try {
        const raw: unknown = e.newValue ? JSON.parse(e.newValue) : null;
        const parsed = this.extractLines(raw);
        const nextLines = parsed?.lines ?? [];

        // Avoid needless emissions
        if (sameCart(this._lines$.value, nextLines)) return;

        this.lastOrigin = parsed?.origin ?? null;
        this._lines$.next(nextLines);
      } catch {
        // ignore
      }
    });
  }

  get snapshot(): CartLine[] {
    return this._lines$.value;
  }

  /** True if the last cart change came from THIS tab (helps UX: avoid auto-opening cart on other tabs). */
  lastChangeIsLocal(): boolean {
    return this.lastOrigin === this.tabId;
  }

  openOverlay(): void {
    this._overlayOpen$.next(true);
  }

  closeOverlay(): void {
    this._overlayOpen$.next(false);
  }

  toggleOverlay(): void {
    this._overlayOpen$.next(!this._overlayOpen$.value);
  }

  add(product: Product, quantity: number): void {
    const q = Math.max(1, Math.floor(quantity));
    const lines = [...this._lines$.value];
    const existing = lines.find(l => l.product.id === product.id);
    if (existing) existing.quantity += q;
    else lines.push({ product, quantity: q });
    this.commit(lines);
  }

  update(productId: string, quantity: number): void {
    const q = Math.max(0, Math.floor(quantity));
    const lines = this._lines$.value
      .map(l => l.product.id === productId ? ({ ...l, quantity: q }) : l)
      .filter(l => l.quantity > 0);
    this.commit(lines);
  }

  remove(productId: string): void {
    this.commit(this._lines$.value.filter(l => l.product.id !== productId));
  }

  clear(): void {
    this.commit([]);
  }

  total(): number {
    return this._lines$.value.reduce((sum, l) => sum + l.product.price * l.quantity, 0);
  }

  private commit(lines: CartLine[]): void {
    this.lastOrigin = this.tabId;
    this._lines$.next(lines);

    const env: CartEnvelope = {
      v: 1,
      origin: this.tabId,
      updatedAt: new Date().toISOString(),
      lines
    };

    this.storage.setJson(CART_KEY, env);
  }

  private getOrCreateTabId(): string {
    const key = 'contoso.tabId.v1';
    let id = '';
    try {
      id = sessionStorage.getItem(key) ?? '';
    } catch {
      id = '';
    }
    if (id) return id;

    const next = (typeof crypto !== 'undefined' && 'randomUUID' in crypto)
      ? (crypto as any).randomUUID()
      : Math.random().toString(16).slice(2);

    try {
      sessionStorage.setItem(key, next);
    } catch {
      // ignore
    }
    return next;
  }

  private extractLines(raw: unknown): { lines: CartLine[]; origin: string | null } | null {
    if (!raw) return null;

    // Legacy: array of lines
    if (Array.isArray(raw)) {
      return { lines: raw as CartLine[], origin: null };
    }

    // Envelope
    if (typeof raw === 'object' && raw !== null) {
      const r = raw as any;
      if (Array.isArray(r.lines)) {
        return { lines: r.lines as CartLine[], origin: typeof r.origin === 'string' ? r.origin : null };
      }
    }

    return null;
  }
}

function sameCart(a: CartLine[], b: CartLine[]): boolean {
  if (a.length !== b.length) return false;

  // Compare by productId + quantity (order-insensitive)
  const mapA = new Map<string, number>();
  for (const l of a) mapA.set(l.product.id, l.quantity);

  for (const l of b) {
    if (!mapA.has(l.product.id)) return false;
    if (mapA.get(l.product.id) !== l.quantity) return false;
  }
  return true;
}
