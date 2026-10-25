import { Routes } from '@angular/router';

import { roleGuard, signedInGuard, signedOutGuard } from './core/auth/guards';
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
      { path: '', pathMatch: 'full', redirectTo: 'tickets' },
      {
        path: 'tickets',
        title: 'Tickets · OpsHub',
        loadComponent: () => import('./features/tickets/ticket-list').then((m) => m.TicketList),
      },
      {
        path: 'tickets/new',
        title: 'New ticket · OpsHub',
        loadComponent: () => import('./features/tickets/ticket-form').then((m) => m.TicketForm),
      },
      {
        path: 'tickets/:id',
        title: 'Ticket · OpsHub',
        loadComponent: () => import('./features/tickets/ticket-detail').then((m) => m.TicketDetail),
      },
      {
        path: 'tickets/:id/edit',
        title: 'Edit ticket · OpsHub',
        loadComponent: () => import('./features/tickets/ticket-form').then((m) => m.TicketForm),
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
