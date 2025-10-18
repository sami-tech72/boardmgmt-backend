import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule, NgOptimizedImage } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';

import { PageHeaderComponent } from '../../shared/page-header/page-header.component';

import { MODULES, Permission, AppModule, toggleFlag } from '@core/models/security.models';
import { RolesService, PermissionDto } from '@core/services/roles.service';
import { MeService } from '@core/services/me.service';

interface CodeVm {
  key: Permission;
  label: string;
  on: boolean;
}
interface ModuleVm {
  key: AppModule;
  title: string;
  icon: string;
  allOn: boolean;
  bits: number;
  codes: CodeVm[];
}

const PERM_ORDER: { key: Permission; label: string }[] = [
  { key: Permission.View, label: 'View' },
  { key: Permission.Create, label: 'Create' },
  { key: Permission.Update, label: 'Update' },
  { key: Permission.Delete, label: 'Delete' },
  { key: Permission.Page, label: 'Page' },
];

@Component({
  selector: 'app-create-role',
  standalone: true,
  imports: [CommonModule, FormsModule, PageHeaderComponent],
  templateUrl: './create-role.component.html',
  styleUrls: ['./create-role.component.scss'],
})
export class CreateRoleComponent {
  private roles = inject(RolesService);
  private me = inject(MeService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toast = inject(ToastrService);

  roleId: string | null = null; // edit mode when present
  roleName = '';
  saving = signal(false);

  modules = signal<ModuleVm[]>(
    MODULES.map((m) => ({
      key: m.key,
      title: m.title,
      icon: m.icon,
      allOn: false,
      bits: 0,
      codes: PERM_ORDER.map((p) => ({ key: p.key, label: p.label, on: false })),
    })),
  );

  constructor() {
    // read query params for edit mode
    const qp = this.route.snapshot.queryParamMap;
    this.roleId = qp.get('roleId');
    const qpName = qp.get('name');
    if (qpName) this.roleName = qpName;

    if (this.roleId) {
      this.roles.getPermissions(this.roleId).subscribe({
        next: (items) => this.applyExisting(items),
        error: () => {}, // ignore
      });
    }
  }

  selectedCount = computed(() =>
    this.modules().reduce((acc, mod) => acc + mod.codes.filter((c) => c.on).length, 0),
  );

  private recompute(mod: ModuleVm) {
    let bits = 0;
    for (const c of mod.codes) bits = toggleFlag(bits, c.key, c.on);
    mod.bits = bits;
    mod.allOn = mod.codes.every((c) => c.on);
  }

  private applyExisting(items: PermissionDto[]) {
    const map = new Map<number, number>();
    for (const it of items) map.set(it.module, it.allowed);

    const updated = this.modules().map((m) => {
      const allowed = map.get(m.key) ?? 0;
      const codes = m.codes.map((c) => ({ ...c, on: (allowed & c.key) === c.key }));
      const next = { ...m, codes };
      this.recompute(next);
      return next;
    });
    this.modules.set(updated);
  }

  toggleAll(mod: ModuleVm) {
    const on = !mod.allOn;
    const updated = this.modules().map((m) => {
      if (m.key !== mod.key) return m;
      const next = { ...m, codes: m.codes.map((c) => ({ ...c, on })) };
      this.recompute(next);
      return next;
    });
    this.modules.set(updated);
  }

  toggleOne(mod: ModuleVm, code: CodeVm) {
    const updated = this.modules().map((m) => {
      if (m.key !== mod.key) return m;
      const next = {
        ...m,
        codes: m.codes.map((c) => (c.key === code.key ? { ...c, on: !c.on } : c)),
      };
      this.recompute(next);
      return next;
    });
    this.modules.set(updated);
  }

  goBack() {
    if (history.length > 1) history.back();
    else this.router.navigate(['/roles']);
  }

  onSubmit() {
    if (this.saving()) return;

    const name = this.roleName.trim();
    if (!name) {
      this.toast.warning('Please enter a role name.');
      return;
    }

    const items: PermissionDto[] = this.modules()
      .filter((m) => m.bits > 0)
      .map((m) => ({ module: m.key, allowed: m.bits }));

    if (items.length === 0) {
      this.toast.warning('Select at least one permission.');
      return;
    }

    this.saving.set(true);

    // EDIT: rename + replace permissions (single call)
    if (this.roleId) {
      this.roles.updateRole(this.roleId, { name, items }).subscribe({
        next: () => {
          this.toast.success('Role updated.');
          this.router.navigate(['/roles']);
          this.saving.set(false);
        },
        error: (err) => {
          console.error(err);
          this.toast.error('Failed to update role.');
          this.saving.set(false);
        },
      });
      return;
    }

    // CREATE: create → set permissions → refresh → navigate
    this.roles.create(name).subscribe({
      next: (created) => {
        this.roles.setPermissions(created.id, items).subscribe({
          next: () => {
            this.me.refreshPermissions().subscribe({
              next: () => {
                this.toast.success('Role created and permissions saved.');
                this.router.navigate(['/roles']);
                this.saving.set(false);
              },
              error: () => {
                this.router.navigate(['/roles']);
                this.saving.set(false);
              },
            });
          },
          error: (err) => {
            console.error(err);
            this.toast.error('Failed to save permissions.');
            this.saving.set(false);
          },
        });
      },
      error: (err) => {
        console.error(err);
        this.toast.error('Failed to create role.');
        this.saving.set(false);
      },
    });
  }
}
