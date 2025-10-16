import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { RolesService, RoleListItem } from '@core/services/roles.service';
import { UserMenuComponent } from '../shared/user-menu/user-menu.component';
import { DataTableDirective } from '../shared/data-table/data-table.directive';

@Component({
  standalone: true,
  selector: 'app-roles',
  imports: [CommonModule, FormsModule, RouterLink, UserMenuComponent, DataTableDirective],
  templateUrl: './roles.page.html',
  styleUrls: ['./roles.page.scss'],
})
export class RolesPage {
  private svc = inject(RolesService);
  private toast = inject(ToastrService);

  roles: RoleListItem[] = [];
  loading = false;
  saving = false;

  editId: string | null = null;
  editName = '';

  ngOnInit() {
    this.refresh();
  }

  refresh() {
    this.loading = true;
    this.svc.getAll().subscribe({
      next: (list) => {
        this.roles = list;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      },
    });
  }

  startEdit(r: RoleListItem) {
    this.editId = r.id;
    this.editName = r.name;
  }
  cancelEdit() {
    this.editId = null;
    this.editName = '';
  }

  saveName(r: RoleListItem) {
    const name = this.editName.trim();
    if (!name) {
      this.toast.warning('Please enter a role name.');
      return;
    }
    if (name === r.name) {
      this.cancelEdit();
      return;
    }

    // If you want name-only, you can call updateRole with current items fetched from API.
    // Simpler: navigate to edit screen and let user save both:
    this.cancelEdit();
    window.location.href = `/roles/create-role?roleId=${encodeURIComponent(
      r.id,
    )}&name=${encodeURIComponent(name)}`;
  }

  remove(r: RoleListItem) {
    if (!confirm(`Delete role "${r.name}"? This cannot be undone.`)) return;
    this.saving = true;
    this.svc.delete(r.id).subscribe({
      next: () => {
        this.roles = this.roles.filter((x) => x.id !== r.id);
        this.toast.success('Role deleted.');
        this.saving = false;
      },
      error: () => {
        this.toast.error('Failed to delete role.');
        this.saving = false;
      },
    });
  }

  countModulesWithPerms(map: Record<number, number> | null | undefined) {
    if (!map) return 0;
    let c = 0;
    for (const k in map) if (map[k] && map[k] !== 0) c++;
    return c;
  }

  onLogout(): void {
    // optional: extra cleanup or message
    console.log('User logged out from dashboard');
    // No need to navigate manually — UserMenuComponent already does that.
  }
}
