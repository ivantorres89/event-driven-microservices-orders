import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { Subscription, firstValueFrom } from 'rxjs';
import { CartService } from '../../core/services/cart.service';
import { OrderProgressService } from '../../core/services/order-progress.service';
import { OrdersService } from '../../core/services/orders.service';
import { SignalRService } from '../../core/services/signalr.service';
import { SpinnerOverlayComponent } from '../../shared/spinner/spinner-overlay.component';
import { ToastService } from '../../core/services/toast.service';
import { environment } from '../../../environments/environment';
import { CartLine, OrderWorkflowStatus } from '../../core/models';

@Component({
  selector: 'app-checkout-page',
  standalone: true,
  imports: [CommonModule, RouterLink, SpinnerOverlayComponent],
  templateUrl: './checkout-page.component.html',
  styleUrls: ['./checkout-page.component.css']
})
export class CheckoutPageComponent implements OnInit, OnDestroy {
  cartLines: CartLine[] = [];
  active = this.progress.snapshot;

  private sub = new Subscription();

  constructor(
    private cart: CartService,
    private progress: OrderProgressService,
    private orders: OrdersService,
    private signalr: SignalRService,
    private toasts: ToastService,
    private router: Router
  ) {}

  async ngOnInit(): Promise<void> {
    this.cartLines = this.cart.snapshot;

    // Make sure we have a live connection on checkout (for reconnection/late-join behavior).
    try {
      await this.signalr.ensureConnected();
    } catch (e) {
      console.warn('SignalR connection failed:', e);
      this.toasts.push('danger', 'SignalR not connected', 'Running in mock/offline mode.');
    }

    this.sub.add(this.progress.activeOrder$.subscribe(a => {
      this.active = a;

      // When completed, add to "Orders" list once.
      if (a?.status === 'Completed' && a.orderId) {
        const already = this.orders.snapshot.some(o => o.id === a.orderId);
        if (!already) {
          this.orders.addFromActive(a);
          this.toasts.push('success', 'Order saved', `Order #${a.orderId} added to Orders.`);
        }
      }
    }));
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  get lines(): Array<{ name: string; qty: number; unitPrice: number; total: number }> {
    const fromActive = this.active?.lines?.length ? this.active.lines : null;
    const src = fromActive
      ? fromActive.map(l => ({ name: l.productName, qty: l.quantity, unitPrice: l.unitPrice }))
      : this.cartLines.map(l => ({ name: l.product.name, qty: l.quantity, unitPrice: l.product.price }));

    return src.map(x => ({ ...x, total: x.qty * x.unitPrice }));
  }

  get total(): number {
    return this.lines.reduce((sum, l) => sum + l.total, 0);
  }

  get canSubmit(): boolean {
    // Only allow submit if we have cart items and no active order in-flight.
    return this.cartLines.length > 0 && !this.active;
  }

  get spinnerVisible(): boolean {
    return !!this.active && this.active.status !== 'Completed';
  }

  get spinnerTitle(): string {
    if (!this.active) return 'Submitting…';
    return this.active.status === 'Accepted'
      ? 'Order accepted…'
      : this.active.status === 'Processing'
      ? 'Processing order…'
      : 'Finishing…';
  }

  get spinnerSubtitle(): string | undefined {
    if (!this.active) return undefined;
    const short = this.active.correlationId.slice(0, 8);
    return `CorrelationId: ${short}…`;
  }

  async submit(): Promise<void> {
    if (!this.canSubmit) return;

    // 1) Call order-accept (mocked by default)
    const lines = [...this.cartLines];

    try {
      const res = await (environment.useMocks
        ? this.orders.submitOrderMock(lines).toPromise()
        : this.orders.submitOrderMock(lines).toPromise()); // Replace with real call later

      if (!res?.correlationId) throw new Error('Missing correlationId');

      // 2) Persist active order and clear cart (cart is ephemeral for demo)
      const active = this.progress.startNew(res.correlationId, lines);
      this.cart.clear();

      // 3) Register correlationId with hub (CorrelationId -> UserId in Redis)
      try {
        await this.signalr.ensureConnected();
        await this.signalr.registerOrder(active.correlationId);
        await this.signalr.ping();
      } catch (e) {
        console.warn('RegisterOrder failed:', e);
      }

      this.toasts.push('info', 'Order submitted', `CorrelationId: ${active.correlationId}`);

      // 4) If mocks enabled, simulate backend progress + completion (so the demo works without services).
      if (environment.useMocks) {
        this.simulateMockWorkflow(active.correlationId);
      }
    } catch (e) {
      console.error(e);
      this.toasts.push('danger', 'Submit failed', 'Could not submit order.');
    }
  }

  private simulateMockWorkflow(correlationId: string): void {
    // Accepted -> Processing -> Completed with pseudo orderId
    setTimeout(() => {
      this.progress.setStatus('Processing', null);
    }, 900);

    setTimeout(() => {
      const orderId = Math.floor(100000 + Math.random() * 900000);
      this.progress.setStatus('Completed', orderId);
    }, 2800);
  }

  done(): void {
    // Clear active order and go to Orders
    this.progress.clear();
    this.router.navigateByUrl('/orders');
  }
}
