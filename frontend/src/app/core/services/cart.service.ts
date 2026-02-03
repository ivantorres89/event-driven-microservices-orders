import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { CartLine, Product } from '../models';

@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly _lines$ = new BehaviorSubject<CartLine[]>([]);
  readonly lines$ = this._lines$.asObservable();

  get snapshot(): CartLine[] {
    return this._lines$.value;
  }

  add(product: Product, quantity: number): void {
    const q = Math.max(1, Math.floor(quantity));
    const lines = [...this._lines$.value];
    const existing = lines.find(l => l.product.id === product.id);
    if (existing) existing.quantity += q;
    else lines.push({ product, quantity: q });
    this._lines$.next(lines);
  }

  update(productId: string, quantity: number): void {
    const q = Math.max(0, Math.floor(quantity));
    const lines = this._lines$.value.map(l => l.product.id === productId ? ({ ...l, quantity: q }) : l)
      .filter(l => l.quantity > 0);
    this._lines$.next(lines);
  }

  remove(productId: string): void {
    this._lines$.next(this._lines$.value.filter(l => l.product.id !== productId));
  }

  clear(): void {
    this._lines$.next([]);
  }

  total(): number {
    return this._lines$.value.reduce((sum, l) => sum + l.product.price * l.quantity, 0);
  }
}
