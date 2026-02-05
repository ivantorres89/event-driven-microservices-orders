import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { Subscription } from 'rxjs';

import { AppHeaderComponent } from './shared/header/app-header.component';
import { AppSidebarComponent } from './shared/sidebar/app-sidebar.component';
import { ToastStackComponent } from './shared/toast/toast-stack.component';
import { CartOverlayComponent } from './shared/cart/cart-overlay.component';

import { SignalRService } from './core/services/signalr.service';
import { CartService } from './core/services/cart.service';
import { OrderProgressService } from './core/services/order-progress.service';
import { CartLine } from './core/models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    AppHeaderComponent,
    AppSidebarComponent,
    ToastStackComponent,
    CartOverlayComponent
  ],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent implements OnInit, OnDestroy {
  cartOpen = false;
  cartLines: CartLine[] = [];
  cartTotal = 0;

  private lastQty = 0;
  private activeStatus: string | null = null;
  private initialized = false;

  private sub = new Subscription();

  constructor(
    private signalr: SignalRService,
    private cart: CartService,
    private progress: OrderProgressService
  ) {}

  async ngOnInit(): Promise<void> {
    // Optional: start connection early. Products/Checkout will also call ensureConnected().
    try {
      await this.signalr.ensureConnected();
    } catch {
      // It's ok in mock mode / offline; the app will still render.
    }

    // Keep local open flag in sync (header button toggles the overlay)
    this.sub.add(this.cart.overlayOpen$.subscribe(open => this.cartOpen = open));

    // Track active status so we can avoid "stale completed order" interfering with a new cart.
    this.sub.add(this.progress.activeOrder$.subscribe(a => {
      this.activeStatus = a?.status ?? null;
    }));

    // Wire cart lines + totals, and auto-open when items are added (only in the tab that did the action).
    this.sub.add(this.cart.lines$.subscribe(lines => {
      this.cartLines = lines;
      this.cartTotal = lines.reduce((sum, l) => sum + l.product.price * l.quantity, 0);

      const qty = lines.reduce((sum, l) => sum + l.quantity, 0);

      // First emission: don't auto-open (covers initial load from localStorage).
      if (!this.initialized) {
        this.initialized = true;
        this.lastQty = qty;
        return;
      }

      // If user starts shopping again after a completed workflow, drop the stale "Active Completed" state.
      if (qty > 0 && this.activeStatus === 'Completed') {
        this.progress.clear();
        this.activeStatus = null;
      }

      // Auto-open overlay only when quantity increases AND the change originated in this tab.
      if (qty > this.lastQty && qty > 0 && this.cart.lastChangeIsLocal()) {
        this.cart.openOverlay();
      }

      // Auto-close if empty.
      if (qty === 0) {
        this.cart.closeOverlay();
      }

      this.lastQty = qty;
    }));
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  closeCart(): void {
    this.cart.closeOverlay();
  }

  onUpdateQty(e: { productId: string; quantity: number }): void {
    this.cart.update(e.productId, e.quantity);
  }

  onRemoveLine(productId: string): void {
    this.cart.remove(productId);
  }
}
