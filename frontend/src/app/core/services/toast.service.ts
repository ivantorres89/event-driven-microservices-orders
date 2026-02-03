import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type ToastKind = 'info' | 'success' | 'danger';

export interface Toast {
  id: string;
  kind: ToastKind;
  title: string;
  message?: string;
  createdAt: number;
}

function uid(): string {
  return Math.random().toString(16).slice(2);
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly _toasts$ = new BehaviorSubject<Toast[]>([]);
  readonly toasts$ = this._toasts$.asObservable();

  push(kind: ToastKind, title: string, message?: string): void {
    const toast: Toast = { id: uid(), kind, title, message, createdAt: Date.now() };
    const next = [toast, ...this._toasts$.value].slice(0, 4);
    this._toasts$.next(next);

    setTimeout(() => {
      const remaining = this._toasts$.value.filter(t => t.id !== toast.id);
      this._toasts$.next(remaining);
    }, 4500);
  }
}
