import {
  Component,
  OnInit,
  inject,
  DestroyRef,
  HostListener,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet, Router, NavigationEnd } from '@angular/router';

import { AccessService } from '@core/services/access.service';
import { AppModule, Permission } from '@core/models/security.models';
import { MeService } from '@core/services/me.service';
import { BROWSER_STORAGE } from '@core/tokens/browser-storage.token';
import { filter } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  standalone: true,
  selector: 'app-shell',
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.component.html',
  styleUrls: ['./shell.component.scss'],
})
export class ShellComponent implements OnInit {
  // expose enums to the template
  AppModule = AppModule;
  Permission = Permission;

  // layout state
  isSidebarOpen = true;
  viewportWidth = 1200;
  activeSection = 'Dashboard';
  breadcrumbs: string[] = ['Dashboard'];

  // DI (Angular 17 style)
  private me = inject(MeService);
  private access = inject(AccessService);
  private storage = inject(BROWSER_STORAGE);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);

  ngOnInit(): void {
    this.viewportWidth = this.getViewportWidth();
    this.isSidebarOpen = this.viewportWidth >= 992;

    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((event) => this.updateNavigationState(event.urlAfterRedirects));

    this.updateNavigationState(this.router.url);

    // load once; MeService is idempotent
    this.me.loadPermissions();
  }

  /** View-permission shortcut for menu items */
  can(module: AppModule) {
    return this.access.can(module, Permission.View);
  }

  toggleSidebar() {
    if (this.viewportWidth < 992) {
      this.isSidebarOpen = !this.isSidebarOpen;
      return;
    }

    this.isSidebarOpen = !this.isSidebarOpen;
  }

  handleNavClick() {
    if (this.viewportWidth < 992) {
      this.isSidebarOpen = false;
    }
  }

  onLogout(e: Event) {
    e.preventDefault();
    this.storage.removeItem('jwt');
    this.router.navigateByUrl('/auth');
  }

  @HostListener('window:resize')
  onWindowResize() {
    this.viewportWidth = this.getViewportWidth();

    if (this.viewportWidth >= 992) {
      this.isSidebarOpen = true;
    }
  }

  private updateNavigationState(url: string) {
    const sanitized = url.split('?')[0];
    const segments = sanitized.split('/').filter(Boolean);

    if (!segments.length || segments[0] === 'dashboard') {
      this.activeSection = 'Dashboard';
      this.breadcrumbs = ['Dashboard'];
      return;
    }

    this.activeSection = this.formatLabel(segments[segments.length - 1]);
    this.breadcrumbs = segments.map((segment) => this.formatLabel(segment));
  }

  private formatLabel(value: string) {
    return value
      .split('-')
      .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
      .join(' ');
  }

  private getViewportWidth(): number {
    return typeof window !== 'undefined' ? window.innerWidth : 1200;
  }
}
