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
  /** All products loaded from API (unfiltered). */
  private allProducts: Product[] = [];

  /** Products displayed after filtering. */
  products: Product[] = [];

  /** Filters. */
  search = '';
  category = '';
  categories: string[] = [];

  /** Quantity per product. */
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

        // Build category list (client-side filters)
        this.categories = [...new Set(list.map(p => p.category).filter(Boolean))];

        // Default quantity should be 1 for every product
        for (const p of list) {
          const v = this.qty[p.id];
          if (!Number.isFinite(v) || (v as number) < 1) this.qty[p.id] = 1;
        }

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

  /** Public (used by template). */
  applyFilters(): void {
    const s = this.search.trim().toLowerCase();

    this.products = this.allProducts.filter(p => {
      const matchesSearch =
        !s ||
        p.name.toLowerCase().includes(s) ||
        (p.vendor ?? '').toLowerCase().includes(s);

      const matchesCategory = !this.category || p.category === this.category;

      return matchesSearch && matchesCategory;
    });

    // Ensure visible products also default to qty=1 (covers edge cases)
    for (const p of this.products) {
      const v = this.qty[p.id];
      if (!Number.isFinite(v) || (v as number) < 1) this.qty[p.id] = 1;
    }
  }

  add(p: Product): void {
    const q = Math.max(1, Math.floor(Number(this.qty[p.id] ?? 1)));
    this.cart.add(p, q);
    this.cart.openOverlay();

    // Reset to default (UX)
    this.qty[p.id] = 1;
  }

  badgeText(p: Product): string | null {
    if (p.discountPercent && p.discountPercent > 0) return `-${p.discountPercent}%`;
    if (p.isSubscription) return 'SUB';
    return null;
  }

  originalPrice(p: Product): number | null {
    const d = Number(p.discountPercent ?? 0);
    if (!d || d <= 0 || d >= 100) return null;
    return p.price / (1 - d / 100);
  }
}
