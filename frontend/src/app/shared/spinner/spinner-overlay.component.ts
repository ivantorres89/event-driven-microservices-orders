import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-spinner-overlay',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './spinner-overlay.component.html',
  styleUrls: ['./spinner-overlay.component.css']
})
export class SpinnerOverlayComponent {
  @Input() visible = false;
  @Input() title = 'Working…';
  @Input() subtitle?: string;
}
