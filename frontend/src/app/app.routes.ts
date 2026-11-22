import { Routes } from '@angular/router';

import { activeGuard, adminGuard, signedOutGuard, statusGuard } from './core/session/guards';

// Every page is lazy-loaded: its code is downloaded only when the route opens
export const routes: Routes = [
  {
    path: 'login',
    title: 'Accedi · Sportlab',
    canActivate: [signedOutGuard],
    loadComponent: () => import('./features/auth/login').then((m) => m.Login),
  },
  {
    path: 'pending',
    title: 'In attesa di approvazione · Sportlab',
    canActivate: [statusGuard('pending')],
    data: { status: 'pending' },
    loadComponent: () => import('./features/auth/account-status').then((m) => m.AccountStatus),
  },
  {
    path: 'disabled',
    title: 'Account disattivato · Sportlab',
    canActivate: [statusGuard('disabled')],
    data: { status: 'disabled' },
    loadComponent: () => import('./features/auth/account-status').then((m) => m.AccountStatus),
  },
  {
    // The signed-in site: the header frame, with the pages inside it
    path: '',
    canActivate: [activeGuard],
    loadComponent: () => import('./features/shell/shell').then((m) => m.Shell),
    children: [
      {
        path: '',
        title: 'Sportlab',
        loadComponent: () => import('./features/home/home').then((m) => m.Home),
      },
      {
        path: 'admin/users',
        title: 'Utenti · Sportlab',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/admin/admin-users').then((m) => m.AdminUsers),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
