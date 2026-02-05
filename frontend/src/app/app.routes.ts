import { Routes } from '@angular/router';
import { ProductsPageComponent } from './pages/products/products-page.component';
import { ProductDetailPageComponent } from './pages/products/product-detail-page.component';
import { CheckoutPageComponent } from './pages/checkout/checkout-page.component';
import { OrdersPageComponent } from './pages/orders/orders-page.component';
import { OrderDetailPageComponent } from './pages/orders/order-detail-page.component';
import { LoginPageComponent } from './pages/login/login-page.component';
import { authGuard } from './core/guards/auth.guard';

export const appRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'products' },
  { path: 'login', component: LoginPageComponent },
  { path: 'products', component: ProductsPageComponent, canActivate: [authGuard] },
  { path: 'products/:id', component: ProductDetailPageComponent, canActivate: [authGuard] },
  { path: 'checkout', component: CheckoutPageComponent, canActivate: [authGuard] },
  { path: 'orders/:id', component: OrderDetailPageComponent, canActivate: [authGuard] },
  { path: 'orders', component: OrdersPageComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: 'products' },
];
