import { Component, computed, effect, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { UsersService, UserDto, RoleOption, DepartmentDto } from './users.service';
import { UserMenuComponent } from '../shared/user-menu/user-menu.component';

declare const bootstrap: any;

@Component({
  standalone: true,
  selector: 'app-users',
  imports: [CommonModule, FormsModule, NgSelectModule,UserMenuComponent],
  templateUrl: './users.page.html',
  styleUrls: ['./users.page.scss'],
})
export class UsersPage {
  // data
  users = signal<UserDto[]>([]);
  total = signal(0);
  isLoading = signal(false);

  roles = signal<RoleOption[]>([]);
  departments = signal<DepartmentDto[]>([]);

  // filters
  q = signal('');
  selectedRoleId = signal<string | null>(null);
  selectedDepartmentId = signal<string | null>(null);
  status = signal<'active' | 'inactive' | ''>('');
  page = signal(1);
  pageSize = signal(20);

  // create
  fullName = '';
  email = '';
  password = '';
  selectedRole = signal<string | null>(null);
  selectedDept = signal<string | null>(null);

  // edit
  editingUser: UserDto | null = null;
  editSelectedRole = signal<string | null>(null);
  editSelectedDept = signal<string | null>(null);        // NEW
  editFullName = '';                                     // NEW
  editEmail = '';                                        // NEW
  editPassword = '';                                     // NEW (optional)
  editIsActive = true;                                   // NEW

  private createModalRef: any | null = null;
  private editModalRef: any | null = null;

  // derived stats (for the current page)
  activeCount = computed(() => this.users().filter(u => u.isActive !== false).length);
  inactiveCount = computed(() => this.users().filter(u => u.isActive === false).length);
  boardMembersCount = computed(() => this.users().filter(u => u.roles?.some(r => /board/i.test(r))).length);
  adminsCount = computed(() => this.users().filter(u => u.roles?.some(r => /admin/i.test(r))).length);

  // client-side inactive filter (backend only has activeOnly)
  filteredForTable = computed(() => {
    const wantInactive = this.status() === 'inactive';
    return this.users().filter(u => (wantInactive ? u.isActive === false : true));
  });

  constructor(private api: UsersService) {
    this.bootstrapData();

    // auto-load whenever filters/page change
    effect(() => { void this.load(); });
  }
prevPage() {
  if (this.page() <= 1) return;
  this.page.update(v => v - 1);
}

nextPage() {
  this.page.update(v => v + 1);
}

  private bootstrapData() {
    this.api.getRoles().subscribe({
      next: r => this.roles.set(r),
      error: () => this.roles.set([]),
    });
    this.api.getDepartments(undefined, true).subscribe({
      next: d => this.departments.set(d),
      error: () => this.departments.set([]),
    });
  }

  private roleNameById = (id: string | null | undefined): string | null =>
    id ? (this.roles().find(r => r.id === id)?.name ?? null) : null;

  private roleIdByName = (name: string | null | undefined): string | null =>
    name ? (this.roles().find(r => r.name.toLowerCase() === name.toLowerCase())?.id ?? null) : null;

  load() {
    this.isLoading.set(true);

    const roleName = this.roleNameById(this.selectedRoleId());
    const roleNames = roleName ? [roleName] : [];
    const activeOnly = this.status() === 'active' ? true : undefined;
    const departmentId = this.selectedDepartmentId();

    this.api.getUsers({
      q: this.q().trim() || undefined,
      page: this.page(),
      pageSize: this.pageSize(),
      activeOnly,
      roleNames,
      departmentId: departmentId || undefined,
    }).subscribe({
      next: (res) => {
        this.users.set(res.items);
        this.total.set(res.total);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  // CREATE
  openCreate() {
    this.fullName = '';
    this.email = '';
    this.password = '';
    this.selectedRole.set(null);
    this.selectedDept.set(null);
    const el = document.getElementById('createUserModal');
    if (el) {
      this.createModalRef = new bootstrap.Modal(el, { backdrop: 'static' });
      this.createModalRef.show();
    }
  }
  closeCreate() { this.createModalRef?.hide?.(); }

  create() {
    if (!this.fullName.trim() || !this.email.trim() || !this.password.trim()) return;

    const [firstName, ...rest] = this.fullName.trim().split(/\s+/);
    const lastName = rest.join(' ');
    const roleName = this.roleNameById(this.selectedRole()); // backend expects NAME
    const departmentId = this.selectedDept();

    this.isLoading.set(true);
    this.api.register({
      firstName,
      lastName,
      email: this.email.trim(),
      password: this.password,
      role: roleName ?? null,
      departmentId: departmentId ?? null,
    }).subscribe({
      next: () => this.finishCreate(),
      error: () => this.isLoading.set(false),
    });
  }
  private finishCreate() {
    this.isLoading.set(false);
    this.closeCreate();
    this.load();
  }

  // EDIT
  openEdit(u: UserDto) {
    this.editingUser = { ...u };

    // Populate form fields
    this.editFullName = u.fullName ?? '';
    this.editEmail = u.email ?? '';
    this.editPassword = '';

    // Role: UI stores ID, backend expects NAME. We convert on save.
    const firstRoleName = (u.roles && u.roles[0]) ?? null;
    this.editSelectedRole.set(this.roleIdByName(firstRoleName));

    // Department
    this.editSelectedDept.set(u.departmentId ?? null);

    // Status
    this.editIsActive = u.isActive !== false;

    // Open modal
    const el = document.getElementById('editUserModal');
    if (el) {
      this.editModalRef = new bootstrap.Modal(el, { backdrop: 'static' });
      this.editModalRef.show();
    }
  }
  

  saveEdit() {
    if (!this.editingUser) return;

    // Split full name like Create
    const [firstName, ...rest] = (this.editFullName ?? '').trim().split(/\s+/);
    const lastName = rest.join(' ');

    // Convert selected role ID -> NAME
    const roleName = this.roleNameById(this.editSelectedRole());
    const departmentId = this.editSelectedDept();

    this.isLoading.set(true);

    this.api.updateUser(this.editingUser.id, {
      firstName,
      lastName,
      email: this.editEmail.trim(),
      newPassword: this.editPassword?.trim() || null, // optional
      role: roleName ?? null,                          // NAME
      departmentId: departmentId ?? null,             // GUID
      isActive: this.editIsActive,
    }).subscribe({
      next: () => this.finishEdit(),
      error: () => this.finishEdit(),
    });
  }

  private finishEdit() {
    this.isLoading.set(false);
    this.closeEdit();
    this.load();
  }

  closeEdit() {
    this.editModalRef?.hide?.();
    this.editingUser = null;
    this.editSelectedRole.set(null);
    this.editSelectedDept.set(null);
    this.editFullName = '';
    this.editEmail = '';
    this.editPassword = '';
    this.editIsActive = true;
  }

  clearFilters() {
    this.q.set('');
    this.selectedRoleId.set(null);
    this.selectedDepartmentId.set(null);
    this.status.set('');
    this.page.set(1);
  }

  statusBadge(u: UserDto) { return u.isActive === false ? 'secondary' : 'success'; }
  trackById(_: number, u: UserDto) { return u.id; }

  onLogout(): void {
    // optional: extra cleanup or message
    console.log('User logged out from dashboard');
    // No need to navigate manually — UserMenuComponent already does that.
  }
}
