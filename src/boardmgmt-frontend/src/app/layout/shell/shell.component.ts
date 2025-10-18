import { Component, OnInit, inject } from '@angular/core';
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
  AppModule = AppModule;
  Permission = Permission;

  private me = inject(MeService);
  private access = inject(AccessService);
  private storage = inject(BROWSER_STORAGE);
  private router = inject(Router);

  mobileSidebarOpen = false;

  ngOnInit(): void {
    this.me.loadPermissions();
  }

  can(module: AppModule) {
    return this.access.can(module, Permission.View);
  }

  toggleMobileSidebar() {
    this.mobileSidebarOpen = !this.mobileSidebarOpen;
  }

  closeMobileSidebar() {
    this.mobileSidebarOpen = false;
  }

  onLogout(e: Event) {
    e.preventDefault();
    this.storage.removeItem('jwt');
    this.router.navigateByUrl('/auth');
  }
}
