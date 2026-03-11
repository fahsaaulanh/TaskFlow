import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from './auth.service';

export const sessionGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.restore().pipe(
    map((user) => user ? true : router.createUrlTree(['/login'])),
    catchError(() => of(router.createUrlTree(['/login'], { queryParams: { unavailable: '1' } }))),
  );
};
