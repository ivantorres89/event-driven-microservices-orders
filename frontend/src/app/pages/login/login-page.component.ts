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
  busy = false;

  constructor(
    private auth: AuthService,
    private signalr: SignalRService,
    private router: Router
  ) {
    this.userId = this.auth.getSuggestedUserId();
  }

  async signIn(): Promise<void> {
    this.error = '';
    const cleaned = (this.userId ?? '').trim();
    if (!cleaned) {
      this.error = 'Please enter a user id.';
      return;
    }

    this.busy = true;
    try {
      // Obtain a signed DEV JWT from order-notification (/dev/token)
      await this.auth.loginDev(cleaned);

      await this.router.navigateByUrl('/products');
    } catch (e: any) {
      console.error(e);
      this.error = e?.message
        ? `Login failed: ${e.message}`
        : 'Login failed. Make sure order-notification is running and CORS is enabled for http://localhost:4200.';
    } finally {
      this.busy = false;
    }
  }

  async signOut(): Promise<void> {
    this.auth.logout();
    await this.signalr.disconnect();
    this.userId = this.auth.getSuggestedUserId();
  }

  generate(): void {
    const rand = Math.random().toString(16).slice(2, 10);
    this.userId = `contoso-user-${rand}`;
  }
}
