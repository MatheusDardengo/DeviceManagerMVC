import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth-guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/login/login').then((m) => m.Login) },
  {
    path: 'devices',
    canActivate: [authGuard],
    loadComponent: () => import('./features/devices/device-list/device-list').then((m) => m.DeviceList),
  },
  { path: '', pathMatch: 'full', redirectTo: 'devices' },
  { path: '**', redirectTo: 'devices' },
];
