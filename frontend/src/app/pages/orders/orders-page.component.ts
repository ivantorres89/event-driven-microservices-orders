import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { OrdersService } from '../../core/services/orders.service';

@Component({
  selector: 'app-orders-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './orders-page.component.html',
  styleUrls: ['./orders-page.component.css']
})
export class OrdersPageComponent implements OnInit {
  loading = true;
  error: string | null = null;
  deletingId: number | null = null;

  constructor(public orders: OrdersService) {}

  ngOnInit(): void {
    this.loading = true;
    this.error = null;

    this.orders.refreshAll(50).subscribe({
      next: () => this.loading = false,
      error: (e) => {
        console.error(e);
        this.loading = false;
        this.error = 'Could not load orders from the API.';
      }
    });
  }

  async delete(id: number): Promise<void> {
    if (!id) return;

    this.deletingId = id;
    this.error = null;

    try {
      await this.orders.deleteOrder(id);
    } catch (e) {
      console.error(e);
      this.error = 'Could not delete the order.';
    } finally {
      this.deletingId = null;
    }
  }

  format(dt: string): string {
    return new Date(dt).toLocaleString();
  }

  total(order: any): number {
    return order.lines.reduce((s: number, l: any) => s + l.unitPrice * l.quantity, 0);
  }
}
