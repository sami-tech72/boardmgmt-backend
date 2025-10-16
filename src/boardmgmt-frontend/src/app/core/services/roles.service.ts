import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '@env/environment';
import { AppModule } from '../models/security.models';

export interface RoleListItem {
  id: string;
  name: string;
  permissions: Record<number, number>;
}
export interface PermissionDto {
  module: AppModule;
  allowed: number;
}
export interface CreateRoleResult {
  id: string;
  name: string;
}
export interface SavedRolePermission {
  id: string;
  module: AppModule;
  allowed: number;
}
export interface UpdateRoleBody {
  name: string;
  items: PermissionDto[];
}

@Injectable({ providedIn: 'root' })
export class RolesService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/roles`;

  getAll() {
    return this.http.get<RoleListItem[]>(this.base);
  }
  create(name: string) {
    return this.http.post<CreateRoleResult>(this.base, { name });
  }
  setPermissions(roleId: string, items: PermissionDto[]) {
    return this.http.put<SavedRolePermission[]>(`${this.base}/permissions`, { roleId, items });
  }
  updateRole(id: string, body: UpdateRoleBody) {
    return this.http.put<void>(`${this.base}/${id}`, body);
  }
  getPermissions(roleId: string) {
    return this.http.get<PermissionDto[]>(`${this.base}/${roleId}/permissions`);
  }

  // ✅ ADD THIS
  delete(id: string) {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
