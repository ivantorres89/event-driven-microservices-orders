import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { SignalRService } from '../../core/services/signalr.service';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './login-page.component.html',
  styleUrls: ['./login-page.component.css']
})
export class LoginPageComponent {
  userId = '';
  error = '';

  constructor(
    private auth: AuthService,
    private signalr: SignalRService,
    private router: Router
  ) {
    this.userId = this.auth.getUserId();
  }

  async signIn(): Promise<void> {
    this.error = '';
    const cleaned = (this.userId ?? '').trim();
    if (!cleaned) {
      this.error = 'Please enter a user id.';
      return;
    }

    // Switch user for the demo
    this.auth.login(cleaned);
    await this.signalr.disconnect();
    await this.router.navigateByUrl('/products');
  }

  async signOut(): Promise<void> {
    this.auth.logout();
    await this.signalr.disconnect();
    this.userId = this.auth.getUserId();
  }

  generate(): void {
    const rand = Math.random().toString(16).slice(2, 10);
    this.userId = `user-${rand}`;
  }
}
