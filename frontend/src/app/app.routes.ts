import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./home/home').then((m) => m.Home), title: 'Pitchside' },
  {
    path: 'overlay/:matchId',
    loadComponent: () => import('./overlay/overlay').then((m) => m.Overlay),
    title: 'Pitchside overlay',
  },
  {
    path: 'control/:matchId',
    loadComponent: () => import('./control/control').then((m) => m.Control),
    title: 'Pitchside control room',
  },
  {
    path: 'viewer/:matchId',
    loadComponent: () => import('./viewer/viewer').then((m) => m.Viewer),
    title: 'Pitchside',
  },
  { path: '**', redirectTo: '' },
];
