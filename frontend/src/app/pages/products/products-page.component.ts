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

type VendorFilter = { vendor: string; count: number };

@Component({
  selector: 'app-products-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, CartOverlayComponent],
  templateUrl: './products-page.component.html',
  styleUrls: ['./products-page.component.css']
})
export class ProductsPageComponent implements OnInit {
  allProducts: Product[] = [];
  products: Product[] = [];
  loading = true;

  // MeasureUp-like UI state
  query = '';
  selectedVendor: string | null = null;
  vendors: VendorFilter[] = [];

  sort: 'position' | 'nameAsc' | 'discountDesc' | 'priceAsc' | 'priceDesc' = 'position';
  pageSize = 12;
  pageIndex = 0;
  total = 0;

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
      this.allProducts = list ?? [];
      this.vendors = this.buildVendors(this.allProducts);

      // default quantities
      for (const p of this.allProducts) this.qty[p.id] = 1;

      this.applyFilters();
      this.loading = false;
    });

    this.cart.lines$.subscribe(lines => {
      this.cartLines = lines;
      this.cartTotal = this.cart.total();
    });
  }

  openCart(): void { this.cartOpen = true; }
  closeCart(): void { this.cartOpen = false; }

  add(p: Product): void {
    const q = Math.max(1, Number(this.qty[p.id] ?? 1));
    this.cart.add(p, q);
    this.cartOpen = true;
  }

  // UI helpers
  setVendor(v: string | null): void {
    this.selectedVendor = v;
    this.pageIndex = 0;
    this.applyFilters();
  }

  onQueryChange(): void {
    this.pageIndex = 0;
    this.applyFilters();
  }

  onSortChange(): void {
    this.pageIndex = 0;
    this.applyFilters();
  }

  setPageSize(size: number): void {
    this.pageSize = Number(size) || 12;
    this.pageIndex = 0;
    this.applyFilters();
  }

  prevPage(): void {
    if (this.pageIndex <= 0) return;
    this.pageIndex--;
    this.applyFilters();
  }

  nextPage(): void {
    const maxPage = Math.max(0, Math.ceil(this.total / this.pageSize) - 1);
    if (this.pageIndex >= maxPage) return;
    this.pageIndex++;
    this.applyFilters();
  }

  get pageLabel(): string {
    if (!this.total) return '0';
    const start = this.pageIndex * this.pageSize + 1;
    const end = Math.min(this.total, start + this.pageSize - 1);
    return `${start}-${end} of ${this.total}`;
  }

  // Pricing display
  originalPrice(p: Product): number | null {
    const d = Number(p.discountPercent ?? 0);
    if (!d || d <= 0 || d >= 100) return null;
    const factor = 1 - (d / 100);
    if (factor <= 0) return null;
    return p.price / factor;
  }

  badgeText(p: Product): string | null {
    const d = Number(p.discountPercent ?? 0);
    if (!d || d <= 0) return null;
    return `-${d}%`;
  }

  // Internals
  private applyFilters(): void {
    const q = (this.query ?? '').trim().toLowerCase();
    const vendor = (this.selectedVendor ?? '').trim().toLowerCase();

    let filtered = [...this.allProducts];

    if (vendor) {
      filtered = filtered.filter(p => (p.vendor ?? '').toLowerCase() === vendor);
    }

    if (q) {
      filtered = filtered.filter(p => {
        const hay = `${p.name} ${p.category} ${p.vendor ?? ''} ${p.id}`.toLowerCase();
        return hay.includes(q);
      });
    }

    // sorting
    switch (this.sort) {
      case 'nameAsc':
        filtered.sort((a,b) => a.name.localeCompare(b.name));
        break;
      case 'discountDesc':
        filtered.sort((a,b) => Number(b.discountPercent ?? 0) - Number(a.discountPercent ?? 0));
        break;
      case 'priceAsc':
        filtered.sort((a,b) => a.price - b.price);
        break;
      case 'priceDesc':
        filtered.sort((a,b) => b.price - a.price);
        break;
      default:
        // position: keep original order
        break;
    }

    this.total = filtered.length;

    const start = this.pageIndex * this.pageSize;
    const end = start + this.pageSize;
    this.products = filtered.slice(start, end);
  }

  private buildVendors(list: Product[]): VendorFilter[] {
    const map = new Map<string, number>();
    for (const p of list) {
      const v = (p.vendor ?? 'Other').trim() || 'Other';
      map.set(v, (map.get(v) ?? 0) + 1);
    }
    return Array.from(map.entries())
      .map(([vendor, count]) => ({ vendor, count }))
      .sort((a,b) => a.vendor.localeCompare(b.vendor));
  }

  // Cart overlay events
  updateQty(productId: string, quantity: number): void {
    this.cart.update(productId, quantity);
  }

  removeLine(productId: string): void {
    this.cart.remove(productId);
  }
}
