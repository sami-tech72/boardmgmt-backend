import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpEvent, HttpRequest, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

export type FolderDto = { id: string; name: string; slug: string; documentCount: number };

export type DocumentDto = {
  id: string;
  originalName: string;
  url: string;
  contentType: string;
  sizeBytes: number;
  version: number;
  folderSlug: string;
  meetingId?: string | null;
  description?: string | null;
  uploadedAt: string; // ISO
};

export type DocType = 'pdf' | 'word' | 'excel' | 'powerpoint';
export type DatePreset = 'today' | 'week' | 'month';

export type RoleItem = {
  id: string;
  name: string;
  display: string;
  accessBit?: number;
  isDefault?: boolean;
};

@Injectable({ providedIn: 'root' })
export class DocumentsService {
  private http = inject(HttpClient);

  // Prefer a real backend URL (e.g. https://localhost:44325/api).
  // If you DO use a proxy, leave environment.apiUrl = '/api'
  // and ensure the proxy sends cookies (changeOrigin + withCredentials true).
  private readonly base =
    (typeof window !== 'undefined' && (window as any)?.env?.apiUrl) ||
    environment.apiUrl ||
    'https://localhost:44325/api';

  // ---------- Folders ----------
  listFolders(): Observable<FolderDto[]> {
    return this.http.get<FolderDto[]>(`${this.base}/folders`, { withCredentials: true });
  }
  createFolder(name: string) {
    return this.http.post<FolderDto>(`${this.base}/folders`, { name }, { withCredentials: true });
  }

  // ---------- Documents (READ) ----------
  listDocuments(opts: { folderSlug?: string; type?: DocType; search?: string; datePreset?: DatePreset; }) {
    let params = new HttpParams();
    if (opts.folderSlug) params = params.set('folderSlug', opts.folderSlug);
    if (opts.type) params = params.set('type', opts.type);
    if (opts.search) params = params.set('search', opts.search);
    if (opts.datePreset) params = params.set('datePreset', opts.datePreset);
    return this.http.get<DocumentDto[]>(`${this.base}/documents`, { params, withCredentials: true });
  }
  getDocumentById(id: string) {
    return this.http.get<DocumentDto>(`${this.base}/documents/${id}`, { withCredentials: true });
  }

  /** Stream a file as Blob (works with auth cookies/CORS) */
  downloadBlob(id: string) {
    return this.http.get(`${this.base}/documents/${id}/download`, {
      responseType: 'blob',
      withCredentials: true
    });
  }

  // ---------- Documents (CREATE) ----------
  createDocuments(
    files: File[],
    meta: { meetingId: string | null; folderSlug: string; description?: string; roleIds?: string[] }
  ): Observable<HttpEvent<any>> {
    const form = new FormData();
    for (const f of files) form.append('files', f, f.name);
    if (meta.meetingId) form.append('meetingId', meta.meetingId);
    form.append('folderSlug', meta.folderSlug || 'root');
    if (meta.description) form.append('description', meta.description);
    if (meta.roleIds?.length) for (const id of meta.roleIds) form.append('roleIds', id);

    const req = new HttpRequest('POST', `${this.base}/documents`, form, {
      reportProgress: true,
      withCredentials: true,
    });
    return this.http.request(req);
  }

  // ---------- Documents (UPDATE) ----------
  updateDocumentMetadata(
    id: string,
    body: { originalName?: string; description?: string | null; folderSlug?: string; roleIds?: string[] | null; }
  ) {
    return this.http.put<DocumentDto>(`${this.base}/documents/${id}`, { id, ...body }, { withCredentials: true });
  }

  updateDocumentViaForm(
    id: string,
    meta: { originalName?: string; description?: string | null; folderSlug?: string; roleIds?: string[] },
    file?: File
  ): Observable<HttpEvent<any>> {
    const form = new FormData();
    if (meta.originalName) form.append('originalName', meta.originalName);
    if (meta.description !== undefined && meta.description !== null) form.append('description', meta.description);
    if (meta.folderSlug) form.append('folderSlug', meta.folderSlug);
    if (meta.roleIds?.length) for (const r of meta.roleIds) form.append('roleIds', r);
    if (file) form.append('file', file, file.name);

    const req = new HttpRequest('PUT', `${this.base}/documents/${id}/form`, form, {
      withCredentials: true,
      reportProgress: true,
    });
    return this.http.request(req);
  }

  // ---------- Documents (DELETE) ----------
  deleteDocument(id: string) {
    return this.http.delete<void>(`${this.base}/documents/${id}`, { withCredentials: true });
  }

  // ---------- Meetings / Roles ----------
  listMeetings(): Observable<{ id: string; title: string; scheduledAt: string }[]> {
    return this.http.get<{ id: string; title: string; scheduledAt: string }[]>(`${this.base}/meetings`, { withCredentials: true });
  }
  listRoles(): Observable<RoleItem[]> {
    return this.http.get<RoleItem[]>(`${this.base}/roles`, { withCredentials: true });
  }
}
