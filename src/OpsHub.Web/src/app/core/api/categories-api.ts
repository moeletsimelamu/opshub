import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { CategoryAdmin } from './models';

export interface UpdateCategoryInput {
  name: string;
  isActive: boolean;
}

/**
 * Typed wrapper around the administrator-only /api/admin/categories endpoints. The ticket
 * forms use GET /api/categories (active categories only) through TicketsApi instead.
 */
@Injectable({ providedIn: 'root' })
export class CategoriesApi {
  private readonly http = inject(HttpClient);

  list(): Observable<CategoryAdmin[]> {
    return this.http.get<CategoryAdmin[]>('/api/admin/categories');
  }

  create(name: string): Observable<CategoryAdmin> {
    return this.http.post<CategoryAdmin>('/api/admin/categories', { name });
  }

  update(id: number, input: UpdateCategoryInput): Observable<CategoryAdmin> {
    return this.http.put<CategoryAdmin>(`/api/admin/categories/${id}`, input);
  }
}
