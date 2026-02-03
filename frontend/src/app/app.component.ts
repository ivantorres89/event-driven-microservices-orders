import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { AppHeaderComponent } from './shared/header/app-header.component';
import { AppSidebarComponent } from './shared/sidebar/app-sidebar.component';
import { ToastStackComponent } from './shared/toast/toast-stack.component';
import { SignalRService } from './core/services/signalr.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, AppHeaderComponent, AppSidebarComponent, ToastStackComponent],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent implements OnInit {
  constructor(private signalr: SignalRService) {}

  async ngOnInit(): Promise<void> {
    // Optional: start connection early. Products page will also call ensureConnected().
    try {
      await this.signalr.ensureConnected();
    } catch {
      // It's ok in mock mode / offline; the app will still render.
    }
  }
}
