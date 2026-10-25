/*
 * TypeScript shapes of the API's JSON. They mirror the C# DTOs in
 * src/OpsHub.Api/Features/** (enums travel as strings).
 */

export type Role = 'Administrator' | 'SupportAgent' | 'Requester';

export type TicketStatus = 'Open' | 'InProgress' | 'WaitingForInformation' | 'Resolved' | 'Closed';

export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export const TICKET_PRIORITIES: readonly TicketPriority[] = ['Low', 'Medium', 'High', 'Critical'];

export const ACTIVE_STATUSES: readonly TicketStatus[] = ['Open', 'InProgress', 'WaitingForInformation'];

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  role: Role;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PersonSummary {
  id: string;
  displayName: string;
}

export interface CategorySummary {
  id: number;
  name: string;
}

export interface AgentSummary {
  id: string;
  displayName: string;
  activeTicketCount: number;
}

export interface TicketListItem {
  id: number;
  reference: string;
  title: string;
  status: TicketStatus;
  priority: TicketPriority;
  category: CategorySummary;
  requester: PersonSummary;
  assignedAgent: PersonSummary | null;
  createdAt: string;
  updatedAt: string;
}

export interface TicketPermissions {
  canEdit: boolean;
  canEditPriority: boolean;
  canAssign: boolean;
  canComment: boolean;
  canAddInternalComment: boolean;
  allowedStatuses: TicketStatus[];
}

export interface TicketDetails extends TicketListItem {
  description: string;
  resolvedAt: string | null;
  version: string;
  permissions: TicketPermissions;
}

export interface TicketComment {
  id: number;
  body: string;
  isInternal: boolean;
  author: PersonSummary;
  authorIsStaff: boolean;
  createdAt: string;
}

export interface StatusHistoryEntry {
  id: number;
  fromStatus: TicketStatus | null;
  toStatus: TicketStatus;
  changedBy: PersonSummary;
  note: string | null;
  changedAt: string;
}

export interface UserSummary {
  id: string;
  email: string;
  displayName: string;
  role: Role;
  isActive: boolean;
  createdAt: string;
}

export const ROLE_LABELS: Record<Role, string> = {
  Administrator: 'Administrator',
  SupportAgent: 'Support agent',
  Requester: 'Requester',
};

export const STATUS_LABELS: Record<TicketStatus, string> = {
  Open: 'Open',
  InProgress: 'In progress',
  WaitingForInformation: 'Waiting for info',
  Resolved: 'Resolved',
  Closed: 'Closed',
};
