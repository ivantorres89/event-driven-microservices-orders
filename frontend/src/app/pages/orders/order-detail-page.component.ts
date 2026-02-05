import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { OrdersService, OrderDto, OrderItemDto } from '../../core/services/orders.service';

type VmItem = OrderItemDto & { lineTotal: number };

@Component({
  selector: 'app-order-detail-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './order-detail-page.component.html',
  styleUrls: ['./order-detail-page.component.css']
})
export class OrderDetailPageComponent implements OnInit {
  loading = true;
  error: string | null = null;

  order: OrderDto | null = null;

  constructor(
    private route: ActivatedRoute,
    private orders: OrdersService
  ) {}

  async ngOnInit(): Promise<void> {
    const idStr = (this.route.snapshot.paramMap.get('id') ?? '').trim();
    const id = Number(idStr);
    if (!idStr || Number.isNaN(id)) {
      this.loading = false;
      this.error = 'Invalid order id.';
      return;
    }

    this.loading = true;
    this.error = null;
    this.order = null;

    try {
      this.order = await firstValueFrom(this.orders.getOrderById(id));
    } catch (e) {
      console.error(e);
      this.error = 'Could not load the order from the API.';
    } finally {
      this.loading = false;
    }
  }

  get items(): VmItem[] {
    if (!this.order) return [];
    return (this.order.items ?? []).map(i => ({ ...i, lineTotal: i.unitPrice * i.quantity }));
  }

  get grandTotal(): number {
    return this.items.reduce((s, i) => s + i.lineTotal, 0);
  }

  format(dt: string): string {
    return new Date(dt).toLocaleString();
  }
}
