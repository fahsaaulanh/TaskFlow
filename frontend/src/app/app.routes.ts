import { Routes } from '@angular/router';
import { sessionGuard } from './features/auth/session.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'board' },
  { path: 'login', loadComponent: () => import('./features/auth/login').then((m) => m.Login) },
  {
    path: 'board',
    canActivate: [sessionGuard],
    loadComponent: () => import('./features/board/board').then((m) => m.Board),
  },
  {
    path: 'insights',
    canActivate: [sessionGuard],
    loadComponent: () => import('./features/insights/insights').then((m) => m.Insights),
  },
  { path: '**', redirectTo: 'board' },
];
