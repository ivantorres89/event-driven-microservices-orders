import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { Product } from '../../core/models';
import { CartService } from '../../core/services/cart.service';
import { ProductsService } from '../../core/services/products.service';

@Component({
  selector: 'app-product-detail-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './product-detail-page.component.html',
  styleUrls: ['./product-detail-page.component.css']
})
export class ProductDetailPageComponent implements OnInit {
  loading = true;
  error: string | null = null;

  product: Product | null = null;
  qty = 1;
  added = false;

  constructor(
    private route: ActivatedRoute,
    private productsApi: ProductsService,
    public cart: CartService,
    private router: Router
  ) {}

  async ngOnInit(): Promise<void> {
    const id = (this.route.snapshot.paramMap.get('id') ?? '').trim();
    if (!id) {
      this.loading = false;
      this.error = 'Product id missing.';
      return;
    }

    this.loading = true;
    this.error = null;
    this.product = null;
    this.added = false;

    try {
      this.product = await firstValueFrom(this.productsApi.getProductById(id));
    } catch (e) {
      console.error(e);
      this.error = 'Could not load the product from the API.';
    } finally {
      this.loading = false;
    }
  }

  add(): void {
    if (!this.product) return;
    const q = Math.max(1, Number(this.qty || 1));
    this.cart.add(this.product, q);
    this.cart.openOverlay();
    this.added = true;
  }

  goToCheckout(): void {
    this.router.navigateByUrl('/checkout');
  }

  originalPrice(p: Product): number | null {
    const d = Number(p.discountPercent ?? 0);
    if (!d || d <= 0 || d >= 100) return null;
    const factor = 1 - (d / 100);
    if (factor <= 0) return null;
    return p.price / factor;
  }
}
