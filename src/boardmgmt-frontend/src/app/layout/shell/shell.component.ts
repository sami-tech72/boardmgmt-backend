import { Component, HostListener, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet, Router } from '@angular/router';

import { AccessService } from '@core/services/access.service';
import { AppModule, Permission } from '@core/models/security.models';
import { MeService } from '@core/services/me.service';
import { BROWSER_STORAGE } from '@core/tokens/browser-storage.token';

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

  sidebarOpened = true;
  isMobile = false;

  // DI (Angular 17 style)
  private me = inject(MeService);
  private access = inject(AccessService);
  private storage = inject(BROWSER_STORAGE);
  private router = inject(Router);

  ngOnInit(): void {
    // load once; MeService is idempotent
    this.me.loadPermissions();
    this.updateViewportState(typeof window !== 'undefined' ? window.innerWidth : 0);
  }

  @HostListener('window:resize')
  onWindowResize() {
    if (typeof window !== 'undefined') {
      this.updateViewportState(window.innerWidth);
    }
  }

  /** View-permission shortcut for menu items */
  can(module: AppModule) {
    return this.access.can(module, Permission.View);
  }

  toggleSidebar() {
    this.sidebarOpened = !this.sidebarOpened;
  }

  closeSidebar() {
    this.sidebarOpened = false;
  }

  closeSidebarOnMobile() {
    if (this.isMobile) {
      this.closeSidebar();
    }
  }

  onLogout(e: Event) {
    e.preventDefault();
    this.storage.removeItem('jwt');
    this.router.navigateByUrl('/auth');
  }

  private updateViewportState(width: number) {
    if (!width) {
      this.sidebarOpened = true;
      this.isMobile = false;
      return;
    }

    this.isMobile = width < 992;
    this.sidebarOpened = this.isMobile ? false : true;
  }
}
