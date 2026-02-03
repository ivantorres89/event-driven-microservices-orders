import { Injectable } from '@angular/core';
import { delay, Observable, of } from 'rxjs';
import { Product } from '../models';
import { MEASUREUP_MOCK_PRODUCTS } from '../mocks/measureup-products.mock';

@Injectable({ providedIn: 'root' })
export class ProductsService {
  getProducts(): Observable<Product[]> {
    // Mock GET /api/products (MeasureUp-like catalog)
    return of(MEASUREUP_MOCK_PRODUCTS).pipe(delay(250));
  }
}
