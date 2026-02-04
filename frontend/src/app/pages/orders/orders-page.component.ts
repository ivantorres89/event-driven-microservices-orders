import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OrdersService } from '../../core/services/orders.service';

@Component({
  selector: 'app-orders-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './orders-page.component.html',
  styleUrls: ['./orders-page.component.css']
})
export class OrdersPageComponent implements OnInit {
  loading = false;
  error = '';
  deletingId: number | null = null;

  constructor(public orders: OrdersService) {}

  ngOnInit(): void {
    // Option A: load ALL orders for the user (backend is paginated; service pulls all pages).
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading = true;
    this.error = '';
    try {
      await this.orders.refreshAll();
    } catch (e: any) {
      // Keep error UI simple; HttpErrorResponse.message is often generic.
      const msg = (e?.error?.title || e?.error?.message || e?.message || '').toString().trim();
      this.error = msg || 'Failed to load orders.';
    } finally {
      this.loading = false;
    }
  }

  async delete(id: number): Promise<void> {
    this.deletingId = id;
    this.error = '';
    try {
      await this.orders.deleteOrder(id);
    } catch (e: any) {
      const msg = (e?.error?.title || e?.error?.message || e?.message || '').toString().trim();
      this.error = msg || 'Failed to delete order.';
    } finally {
      this.deletingId = null;
    }
  }

  format(dt: string): string {
    const d = new Date(dt);
    return d.toLocaleString();
  }

  total(order: any): number {
    return order.lines.reduce((s: number, l: any) => s + l.unitPrice * l.quantity, 0);
  }
}
