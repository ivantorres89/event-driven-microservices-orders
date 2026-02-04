import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { delay, EMPTY, expand, map, Observable, of, reduce } from 'rxjs';
import { Product } from '../models';
import { MEASUREUP_MOCK_PRODUCTS } from '../mocks/measureup-products.mock';
import { RuntimeConfigService } from './runtime-config.service';

type PageEnvelope<T> = {
  offset: number;
  size: number;
  total: number;
  items: T[];
};

type ProductDto = {
  externalProductId: string;
  name: string;
  category: string;
  vendor?: string;
  imageUrl?: string;
  discount?: number;
  billingPeriod?: string;
  isSubscription?: boolean;
  price: number;
};

@Injectable({ providedIn: 'root' })
export class ProductsService {
  constructor(
    private http: HttpClient,
    private cfg: RuntimeConfigService
  ) {}

  /**
   * Master view data source.
   * Uses the real paged API (GET /api/products?offset=&size=).
   * For the current SPA UX (filters/search), we load all pages once and keep client-side filtering.
   */
  getProducts(): Observable<Product[]> {
    if (this.cfg.useMocks) {
      return of(MEASUREUP_MOCK_PRODUCTS).pipe(delay(250));
    }

    return this.fetchAllProducts(100).pipe(map(list => list.map(dtoToProduct)));
  }

  /** Detail view data source: GET /api/products/{id} */
  getProductById(id: string): Observable<Product> {
    if (this.cfg.useMocks) {
      const hit = MEASUREUP_MOCK_PRODUCTS.find(p => p.id === id);
      if (!hit) throw new Error('Product not found');
      return of(hit).pipe(delay(150));
    }

    const base = this.cfg.orderAcceptApiBaseUrl.replace(/\/$/, '');
    const url = `${base}/api/products/${encodeURIComponent(id)}`;
    return this.http.get<ProductDto>(url).pipe(map(dtoToProduct));
  }

  private fetchProductsPage(offset: number, size: number): Observable<PageEnvelope<ProductDto>> {
    const base = this.cfg.orderAcceptApiBaseUrl.replace(/\/$/, '');
    const url = `${base}/api/products?offset=${offset}&size=${size}`;
    return this.http.get<PageEnvelope<ProductDto>>(url);
  }

  /**
   * Loads all pages by repeatedly calling GET /api/products?offset=&size=
   * until offset + size >= total.
   */
  private fetchAllProducts(pageSize: number): Observable<ProductDto[]> {
    return this.fetchProductsPage(0, pageSize).pipe(
      expand((page) => {
        const nextOffset = (page.offset ?? 0) + (page.size ?? pageSize);
        if (nextOffset >= (page.total ?? 0)) return EMPTY;
        return this.fetchProductsPage(nextOffset, pageSize);
      }),
      reduce((acc, page) => acc.concat(page.items ?? []), [] as ProductDto[])
    );
  }
}

function dtoToProduct(dto: ProductDto): Product {
  return {
    id: dto.externalProductId,
    name: dto.name,
    category: dto.category,
    vendor: dto.vendor,
    imageUrl: dto.imageUrl,
    discountPercent: dto.discount ?? 0,
    billingPeriod: dto.billingPeriod,
    isSubscription: dto.isSubscription ?? false,
    price: dto.price,
    stock: 999
  };
}
