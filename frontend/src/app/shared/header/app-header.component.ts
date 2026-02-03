import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { SignalRService } from '../../core/services/signalr.service';
import { AuthService } from '../../core/services/auth.service';
import { OrderProgressService } from '../../core/services/order-progress.service';

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

  private sub = new Subscription();

  constructor(
    private signalr: SignalRService,
    private auth: AuthService,
    private progress: OrderProgressService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.sub.add(this.auth.userId$.subscribe(uid => this.userId = uid));
    this.sub.add(this.signalr.state$.subscribe(s => this.state = s));
    this.sub.add(this.progress.activeOrder$.subscribe(a => {
      this.active = a ? { status: a.status, orderId: a.orderId, correlationId: a.correlationId } : null;
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

  async signOut(): Promise<void> {
    this.auth.logout();
    await this.signalr.disconnect();
    await this.router.navigateByUrl('/login');
  }
}
