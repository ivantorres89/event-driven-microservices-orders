import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { Product } from '../../core/models';
import { ProductsService } from '../../core/services/products.service';
import { CartService } from '../../core/services/cart.service';

@Component({
  selector: 'app-products-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './products-page.component.html',
  styleUrls: ['./products-page.component.css']
})
export class ProductsPageComponent implements OnInit {

  /** all products loaded from API */
  private allProducts: Product[] = [];

  /** products displayed after filtering */
  products: Product[] = [];

  /** filters */
  search = '';
  category = '';
  categories: string[] = [];

  /** qty per product */
  qty: Record<string, number> = {};

  loading = true;
  error: string | null = null;

  constructor(
    private productsApi: ProductsService,
    private cart: CartService
  ) {}

  ngOnInit(): void {
    this.loading = true;
    this.error = null;

    this.productsApi.getProducts().subscribe({
      next: (list) => {
        this.allProducts = list;
        this.categories = [...new Set(list.map(p => p.category).filter(Boolean))];
        this.applyFilters();
        this.loading = false;
      },
      error: (e) => {
        console.error(e);
        this.loading = false;
        this.error = 'Could not load products from the API.';
      }
    });
  }

  /** MUST be public – used by the template */
  applyFilters(): void {
    const s = this.search.trim().toLowerCase();

    this.products = this.allProducts.filter(p => {
      const matchesSearch =
        !s ||
        p.name.toLowerCase().includes(s) ||
        (p.vendor ?? '').toLowerCase().includes(s);

      const matchesCategory =
        !this.category || p.category === this.category;

      return matchesSearch && matchesCategory;
    });
  }

  add(p: Product): void {
    const q = Math.max(1, Number(this.qty[p.id] || 1));
    this.cart.add(p, q);
    this.qty[p.id] = 1;
  }

  badgeText(p: Product): string | null {
    if (p.discountPercent && p.discountPercent > 0) {
      return `-${p.discountPercent}%`;
    }
    if (p.isSubscription) {
      return 'SUB';
    }
    return null;
  }

  originalPrice(p: Product): number | null {
    const d = Number(p.discountPercent ?? 0);
    if (!d || d <= 0 || d >= 100) return null;
    return p.price / (1 - d / 100);
  }
}
