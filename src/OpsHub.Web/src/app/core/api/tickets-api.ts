import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  AgentSummary,
  CategorySummary,
  Dashboard,
  PagedResult,
  StatusHistoryEntry,
  TicketComment,
  TicketDetails,
  TicketListItem,
  TicketPriority,
  TicketStatus,
} from './models';

export interface TicketListParams {
  status?: readonly TicketStatus[];
  priority?: readonly TicketPriority[];
  categoryId?: number;
  assigneeId?: string;
  unassigned?: boolean;
  search?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

export interface TicketInput {
  title: string;
  description: string;
  categoryId: number;
  priority: TicketPriority;
}

/** Thin, typed wrapper around the /api/tickets endpoints. No state, no caching. */
@Injectable({ providedIn: 'root' })
export class TicketsApi {
  private readonly http = inject(HttpClient);

  list(params: TicketListParams): Observable<PagedResult<TicketListItem>> {
    let query = new HttpParams();
    for (const status of params.status ?? []) query = query.append('status', status);
    for (const priority of params.priority ?? []) query = query.append('priority', priority);
    if (params.categoryId) query = query.set('categoryId', params.categoryId);
    if (params.assigneeId) query = query.set('assigneeId', params.assigneeId);
    if (params.unassigned) query = query.set('unassigned', true);
    if (params.search) query = query.set('search', params.search);
    if (params.sortBy) query = query.set('sortBy', params.sortBy);
    if (params.sortDirection) query = query.set('sortDirection', params.sortDirection);
    if (params.page) query = query.set('page', params.page);
    if (params.pageSize) query = query.set('pageSize', params.pageSize);

    return this.http.get<PagedResult<TicketListItem>>('/api/tickets', { params: query });
  }

  get(id: number): Observable<TicketDetails> {
    return this.http.get<TicketDetails>(`/api/tickets/${id}`);
  }

  create(input: TicketInput): Observable<TicketDetails> {
    return this.http.post<TicketDetails>('/api/tickets', input);
  }

  update(id: number, input: TicketInput, version: string): Observable<TicketDetails> {
    return this.http.put<TicketDetails>(`/api/tickets/${id}`, { ...input, version });
  }

  assign(id: number, agentId: string | null, version: string): Observable<TicketDetails> {
    return this.http.put<TicketDetails>(`/api/tickets/${id}/assignee`, { agentId, version });
  }

  changeStatus(id: number, status: TicketStatus, note: string | null, version: string): Observable<TicketDetails> {
    return this.http.put<TicketDetails>(`/api/tickets/${id}/status`, { status, note, version });
  }

  comments(id: number): Observable<TicketComment[]> {
    return this.http.get<TicketComment[]>(`/api/tickets/${id}/comments`);
  }

  addComment(id: number, body: string, isInternal: boolean): Observable<TicketComment> {
    return this.http.post<TicketComment>(`/api/tickets/${id}/comments`, { body, isInternal });
  }

  history(id: number): Observable<StatusHistoryEntry[]> {
    return this.http.get<StatusHistoryEntry[]>(`/api/tickets/${id}/history`);
  }

  categories(): Observable<CategorySummary[]> {
    return this.http.get<CategorySummary[]>('/api/categories');
  }

  agents(): Observable<AgentSummary[]> {
    return this.http.get<AgentSummary[]>('/api/agents');
  }

  /** Staff-only service-desk overview, computed from live data on every call. */
  dashboard(): Observable<Dashboard> {
    return this.http.get<Dashboard>('/api/dashboard');
  }
}
