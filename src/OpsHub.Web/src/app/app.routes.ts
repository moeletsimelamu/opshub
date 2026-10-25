import { inject } from '@angular/core';
import { Routes } from '@angular/router';

import { roleGuard, signedInGuard, signedOutGuard } from './core/auth/guards';
import { SessionService } from './core/auth/session.service';
import { unsavedChangesGuard } from './core/unsaved-changes';
import { Shell } from './layout/shell';

/*
 * Screens are lazy-loaded (loadComponent), so the login page doesn't download the
 * ticket screens' code until someone signs in.
 */
export const routes: Routes = [
  {
    path: 'login',
    canActivate: [signedOutGuard],
    title: 'Sign in · OpsHub',
    loadComponent: () => import('./features/auth/login').then((m) => m.Login),
  },
  {
    path: '',
    component: Shell,
    canActivate: [signedInGuard],
    children: [
      // Staff start on the dashboard, requesters on their tickets.
      { path: '', pathMatch: 'full', redirectTo: () => inject(SessionService).homeUrl() },
      {
        path: 'dashboard',
        title: 'Dashboard · OpsHub',
        canActivate: [roleGuard],
        data: { roles: ['Administrator', 'SupportAgent'] },
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.DashboardPage),
      },
      {
        path: 'tickets',
        title: 'Tickets · OpsHub',
        loadComponent: () => import('./features/tickets/ticket-list').then((m) => m.TicketList),
      },
      {
        path: 'tickets/new',
        canDeactivate: [unsavedChangesGuard],
        title: 'New ticket · OpsHub',
        loadComponent: () => import('./features/tickets/ticket-form').then((m) => m.TicketForm),
      },
      {
        path: 'tickets/:id',
        canDeactivate: [unsavedChangesGuard],
        title: 'Ticket · OpsHub',
        loadComponent: () => import('./features/tickets/ticket-detail').then((m) => m.TicketDetail),
      },
      {
        path: 'tickets/:id/edit',
        canDeactivate: [unsavedChangesGuard],
        title: 'Edit ticket · OpsHub',
        loadComponent: () => import('./features/tickets/ticket-form').then((m) => m.TicketForm),
      },
      {
        path: 'account',
        title: 'Your account · OpsHub',
        loadComponent: () => import('./features/account/account').then((m) => m.Account),
      },
      {
        path: 'admin/users',
        title: 'Users · OpsHub',
        canActivate: [roleGuard],
        data: { roles: ['Administrator'] },
        loadComponent: () => import('./features/admin/user-list').then((m) => m.UserList),
      },
      {
        path: '**',
        title: 'Not found · OpsHub',
        loadComponent: () => import('./layout/not-found').then((m) => m.NotFound),
      },
    ],
  },
];
