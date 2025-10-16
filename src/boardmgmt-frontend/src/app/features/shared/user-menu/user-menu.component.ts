import { Component, Input, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { BROWSER_STORAGE } from '@core/tokens/browser-storage.token';

@Component({
  standalone: true,
  selector: 'app-user-menu',
  imports: [CommonModule, RouterLink],
  templateUrl: './user-menu.component.html',
})
export class UserMenuComponent {
  @Input() displayName = '';
  @Input() profileHref = '/profile';
  @Input() settingsHref = '/settings';
  @Input() logoutHref = '/auth';
  @Output() logout = new EventEmitter<void>();

  private storage = inject(BROWSER_STORAGE);
  private router = inject(Router);

  ngOnInit() {
    const token = this.storage.getItem('jwt');
    if (token) {
      const user = this.decodeJwt(token);
      if (user) {
        
        // Try name, then email, otherwise fallback
        this.displayName = user.name || user.fullName || user.email || user.unique_name || 'User';
      } else {
        this.displayName = 'User';
      }
    } else {
      this.displayName = 'User';
    }
  }

  private decodeJwt(token: string): any | null {
    try {
      const base64Url = token.split('.')[1];
      const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
      const jsonPayload = decodeURIComponent(
        atob(base64)
          .split('')
          .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
          .join('')
      );
      return JSON.parse(jsonPayload);
    } catch (e) {
      console.error('JWT decode failed', e);
      return null;
    }
  }

  onLogoutClick(e: Event) {
    e.preventDefault();
    this.storage.removeItem('jwt');
    this.logout.emit();
    this.router.navigateByUrl('/auth');
  }
}
