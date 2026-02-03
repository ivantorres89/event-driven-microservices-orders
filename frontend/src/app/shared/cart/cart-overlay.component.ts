import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { CartLine } from '../../core/models';

@Component({
  selector: 'app-cart-overlay',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './cart-overlay.component.html',
  styleUrls: ['./cart-overlay.component.css']
})
export class CartOverlayComponent {
  @Input() open = false;
  @Input() lines: CartLine[] = [];
  @Input() total = 0;

  @Output() close = new EventEmitter<void>();
  @Output() updateQty = new EventEmitter<{ productId: string; quantity: number }>();
  @Output() removeLine = new EventEmitter<string>();

  constructor(private router: Router) {}

  goCheckout(): void {
    this.close.emit();
    this.router.navigateByUrl('/checkout');
  }
}
