import { Injectable } from '@angular/core';
import { delay, Observable, of } from 'rxjs';
import { Product } from '../models';

@Injectable({ providedIn: 'root' })
export class ProductsService {
  getProducts(): Observable<Product[]> {
    // Mock GET /api/products
    const products: Product[] = [
      { id: 'SUR-PRO-11', name: 'Surface Pro 11', category: 'Devices', price: 1299, stock: 24 },
      { id: 'SUR-LAP-6', name: 'Surface Laptop 6', category: 'Devices', price: 1499, stock: 12 },
      { id: 'XBX-SER-X', name: 'Xbox Series X', category: 'Gaming', price: 549, stock: 18 },
      { id: 'MS-365-PER', name: 'Microsoft 365 Personal (1y)', category: 'Software', price: 69, stock: 999 },
      { id: 'AZ-CRD-100', name: 'Azure Credit €100', category: 'Cloud', price: 100, stock: 999 },
      { id: 'HLD-SSD-1T', name: 'External SSD 1TB', category: 'Accessories', price: 129, stock: 60 },
    ];

    return of(products).pipe(delay(250));
  }
}
