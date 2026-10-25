import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { PagedResult, Role, UserSummary } from './models';

export interface UserListParams {
  search?: string;
  role?: Role;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}

export interface CreateUserInput {
  email: string;
  displayName: string;
  role: Role;
  password: string;
}

export interface UpdateUserInput {
  displayName: string;
  role: Role;
  isActive: boolean;
}

/** Typed wrapper around the administrator-only /api/users endpoints. */
@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);

  list(params: UserListParams): Observable<PagedResult<UserSummary>> {
    let query = new HttpParams();
    if (params.search) query = query.set('search', params.search);
    if (params.role) query = query.set('role', params.role);
    if (params.isActive !== undefined) query = query.set('isActive', params.isActive);
    if (params.page) query = query.set('page', params.page);
    if (params.pageSize) query = query.set('pageSize', params.pageSize);
    return this.http.get<PagedResult<UserSummary>>('/api/users', { params: query });
  }

  create(input: CreateUserInput): Observable<UserSummary> {
    return this.http.post<UserSummary>('/api/users', input);
  }

  update(id: string, input: UpdateUserInput): Observable<UserSummary> {
    return this.http.put<UserSummary>(`/api/users/${id}`, input);
  }
}
