import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ProductsService } from '../../core/services/products.service';
import { CartService } from '../../core/services/cart.service';
import { SignalRService } from '../../core/services/signalr.service';
import { CartOverlayComponent } from '../../shared/cart/cart-overlay.component';
import { OrderProgressService } from '../../core/services/order-progress.service';
import { Product } from '../../core/models';

@Component({
  selector: 'app-products-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, CartOverlayComponent],
  templateUrl: './products-page.component.html',
  styleUrls: ['./products-page.component.css']
})
export class ProductsPageComponent implements OnInit {
  products: Product[] = [];
  loading = true;

  cartOpen = false;
  cartLines = this.cart.snapshot;
  cartTotal = this.cart.total();

  qty: Record<string, number> = {};

  constructor(
    private productsApi: ProductsService,
    public cart: CartService,
    private signalr: SignalRService,
        public progress: OrderProgressService
  ) {}

  async ngOnInit(): Promise<void> {
    // Requirement: entering Products view connects to SignalR hub
    await this.signalr.ensureConnected();

    this.productsApi.getProducts().subscribe(list => {
      this.products = list;
      this.loading = false;
      for (const p of list) this.qty[p.id] = 1;
    });

    this.cart.lines$.subscribe(lines => {
      this.cartLines = lines;
      this.cartTotal = this.cart.total();
    });
  }

  openCart(): void { this.cartOpen = true; }
  closeCart(): void { this.cartOpen = false; }

  add(p: Product): void {
    const q = this.qty[p.id] ?? 1;
    this.cart.add(p, q);
    this.cartOpen = true;
  }

  updateQty(productId: string, quantity: number): void {
    this.cart.update(productId, quantity);
  }

  removeLine(productId: string): void {
    this.cart.remove(productId);
  }
}
