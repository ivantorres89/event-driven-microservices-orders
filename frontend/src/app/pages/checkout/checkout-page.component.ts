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
import { RuntimeConfigService } from '../../core/services/runtime-config.service';
import { ActiveOrder, CartLine } from '../../core/models';

@Component({
  selector: 'app-checkout-page',
  standalone: true,
  imports: [CommonModule, RouterLink, SpinnerOverlayComponent],
  templateUrl: './checkout-page.component.html',
  styleUrls: ['./checkout-page.component.css']
})
export class CheckoutPageComponent implements OnInit, OnDestroy {
  cartLines: CartLine[] = [];
  active: ActiveOrder | null = this.progress.snapshot;

  private sub = new Subscription();
  private navigatedToDetail = false;

  constructor(
    private cart: CartService,
    private progress: OrderProgressService,
    private orders: OrdersService,
    private signalr: SignalRService,
    private toasts: ToastService,
    private router: Router,
    private cfg: RuntimeConfigService
  ) {}

  async ngOnInit(): Promise<void> {
    // Keep cart in sync (so checkout reflects real selection)
    this.cartLines = this.cart.snapshot;
    this.sub.add(this.cart.lines$.subscribe(lines => this.cartLines = lines));

    try {
      await this.signalr.ensureConnected();
    } catch (e) {
      console.warn('SignalR connection failed:', e);
      this.toasts.push('danger', 'SignalR not connected', 'Running in mock/offline mode.');
    }

    this.sub.add(this.progress.activeOrder$.subscribe(a => {
      this.active = a;

      // When workflow completes, ensure it's visible in Orders list (local) at least once.
      if (a?.status === 'Completed' && a.orderId) {
        const already = this.orders.snapshot.some(o => o.id === a.orderId);
        if (!already) {
          this.orders.addFromActive(a);
          this.toasts.push('success', 'Order saved', `Order #${a.orderId} added to Orders.`);
        }

        // UX: jump straight to the read-only order detail when the workflow completes.
        if (!this.navigatedToDetail) {
          this.navigatedToDetail = true;
          this.router.navigateByUrl(`/orders/${a.orderId}`);
        }
      }
    }));
  }

  isCompletedStatus(status: string | null | undefined): boolean {
    return status === 'Completed';
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  /** Active order only blocks checkout while it's in-flight (Accepted/Processing). */
  private get activeInFlight(): ActiveOrder | null {
    return this.active && this.active.status !== 'Completed' ? this.active : null;
  }

  get lines(): Array<{ name: string; qty: number; unitPrice: number; total: number }> {
    // BUGFIX: do NOT render lines from a completed workflow; show current cart selection instead.
    const fromActive = this.activeInFlight?.lines?.length ? this.activeInFlight.lines : null;

    const src = fromActive
      ? fromActive.map(l => ({ name: l.productName, qty: l.quantity, unitPrice: l.unitPrice }))
      : this.cartLines.map(l => ({ name: l.product.name, qty: l.quantity, unitPrice: l.product.price }));

    return src.map(x => ({ ...x, total: x.qty * x.unitPrice }));
  }

  get total(): number {
    return this.lines.reduce((sum, l) => sum + l.total, 0);
  }

  get canSubmit(): boolean {
    // BUGFIX: allow submitting a new order even if there's a stale completed "active" order.
    return this.cartLines.length > 0 && !this.activeInFlight;
  }

  get spinnerVisible(): boolean {
    // Spinner only while in-flight
    return !!this.activeInFlight;
  }

  get spinnerTitle(): string {
    const a = this.activeInFlight;
    if (!a) return 'Submitting…';
    return a.status === 'Accepted'
      ? 'Order accepted…'
      : a.status === 'Processing'
      ? 'Processing order…'
      : 'Finishing…';
  }

  get spinnerSubtitle(): string | undefined {
    const a = this.activeInFlight;
    if (!a) return undefined;
    const short = a.correlationId.slice(0, 8);
    return `CorrelationId: ${short}…`;
  }

  async submit(): Promise<void> {
    if (!this.canSubmit) return;

    const lines = [...this.cartLines];

    try {
      // 1) POST /api/orders
      const created = await firstValueFrom(this.orders.createOrder(lines));
      if (!created?.correlationId) throw new Error('Missing correlationId');

      // 2) Persist active + clear cart
      const active = this.progress.startNew(created.correlationId, lines);
      this.progress.setStatus('Accepted', created.id);
      this.cart.clear();

      // 3) Register correlationId with hub
      try {
        await this.signalr.ensureConnected();
        await this.signalr.registerOrder(active.correlationId);
        await this.signalr.ping();
      } catch (e) {
        console.warn('RegisterOrder failed:', e);
      }

      this.toasts.push('info', 'Order submitted', `Order #${created.id} (CorrelationId: ${active.correlationId})`);

      // 4) Mocks: simulate workflow
      if (this.cfg.useMocks) {
        this.simulateMockWorkflow(active.correlationId);
      }
    } catch (e) {
      console.error(e);
      this.toasts.push('danger', 'Submit failed', 'Could not submit order.');
    }
  }

  private simulateMockWorkflow(correlationId: string): void {
    setTimeout(() => this.progress.setStatus('Processing', null), 900);
    setTimeout(() => {
      const orderId = Math.floor(100000 + Math.random() * 900000);
      this.progress.setStatus('Completed', orderId);
    }, 2800);
  }



  viewOrderDetail(): void {
    if (this.active?.orderId) {
      this.router.navigateByUrl(`/orders/${this.active.orderId}`);
    }
  }

  done(): void {
    this.progress.clear();
    this.router.navigateByUrl('/orders');
  }
}
