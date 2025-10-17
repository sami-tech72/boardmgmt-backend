import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
  Router,
  NavigationEnd,
} from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs/operators';

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

  // DI (Angular 17 style)
  private me = inject(MeService);
  private access = inject(AccessService);
  private storage = inject(BROWSER_STORAGE);
  private router = inject(Router);

  sidebarOpen = false;

  ngOnInit(): void {
    // load once; MeService is idempotent
    this.me.loadPermissions();

    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed()
      )
      .subscribe(() => {
        this.sidebarOpen = false;
      });
  }

  /** View-permission shortcut for menu items */
  can(module: AppModule) {
    return this.access.can(module, Permission.View);
  }

  toggleSidebar() {
    this.sidebarOpen = !this.sidebarOpen;
  }

  closeSidebar() {
    this.sidebarOpen = false;
  }

  onLogout(e: Event) {
    e.preventDefault();
    this.storage.removeItem('jwt');
    this.router.navigateByUrl('/auth');
  }
}
