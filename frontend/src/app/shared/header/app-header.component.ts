import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { SignalRService } from '../../core/services/signalr.service';
import { AuthService } from '../../core/services/auth.service';
import { OrderProgressService } from '../../core/services/order-progress.service';
import { CartService } from '../../core/services/cart.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './app-header.component.html',
  styleUrls: ['./app-header.component.css']
})
export class AppHeaderComponent implements OnInit, OnDestroy {
  state: 'disconnected' | 'connecting' | 'connected' = 'disconnected';
  userId = '';
  active: { status: string; orderId: number | null; correlationId: string } | null = null;

  cartCount = 0;
  cartTotal = 0;

  private sub = new Subscription();

  constructor(
    private signalr: SignalRService,
    private auth: AuthService,
    private progress: OrderProgressService,
    public cart: CartService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.sub.add(this.auth.userId$.subscribe(uid => this.userId = uid));
    this.sub.add(this.signalr.state$.subscribe(s => this.state = s));
    this.sub.add(this.progress.activeOrder$.subscribe(a => {
      this.active = a ? { status: a.status, orderId: a.orderId, correlationId: a.correlationId } : null;
    }));

    this.sub.add(this.cart.lines$.subscribe(lines => {
      this.cartCount = lines.reduce((n, l) => n + l.quantity, 0);
      this.cartTotal = lines.reduce((sum, l) => sum + l.product.price * l.quantity, 0);
    }));
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  get dotClass(): string {
    if (this.state === 'connected') return 'dot success';
    if (this.state === 'connecting') return 'dot';
    return 'dot danger';
  }

  toggleCart(): void {
    this.cart.toggleOverlay();
  }

  async signOut(): Promise<void> {
    this.auth.logout();
    await this.signalr.disconnect();
    await this.router.navigateByUrl('/login');
  }
}
