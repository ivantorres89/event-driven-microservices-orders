import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OrdersService } from '../../core/services/orders.service';

@Component({
  selector: 'app-orders-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './orders-page.component.html',
  styleUrls: ['./orders-page.component.css']
})
export class OrdersPageComponent {
  constructor(public orders: OrdersService) {}

  async delete(id: number): Promise<void> {
    await this.orders.deleteOrder(id);
  }

  format(dt: string): string {
    const d = new Date(dt);
    return d.toLocaleString();
  }

  total(order: any): number {
    return order.lines.reduce((s: number, l: any) => s + l.unitPrice * l.quantity, 0);
  }
}
