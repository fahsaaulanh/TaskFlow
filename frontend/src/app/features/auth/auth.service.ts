import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { catchError, of, switchMap, tap, throwError } from 'rxjs';

export interface SessionUser {
  id: number;
  email: string;
  displayName: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly session = signal<SessionUser | null>(null);
  readonly user = this.session.asReadonly();

  restore() {
    return this.http.get<SessionUser>('/api/auth/session').pipe(
      catchError((error: HttpErrorResponse) =>
        error.status === 401 ? of(null) : throwError(() => error),
      ),
      tap((user) => this.session.set(user)),
    );
  }

  login(email: string, password: string) {
    return this.http.get<void>('/api/auth/csrf').pipe(
      switchMap(() => this.http.post<SessionUser>('/api/auth/login', { email, password })),
      tap((user) => this.session.set(user)),
    );
  }

  logout() {
    return this.http.get<void>('/api/auth/csrf').pipe(
      switchMap(() => this.http.post<void>('/api/auth/logout', null)),
      catchError((error: HttpErrorResponse) =>
        error.status === 401 ? of(undefined) : throwError(() => error),
      ),
      tap(() => this.session.set(null)),
    );
  }
}
