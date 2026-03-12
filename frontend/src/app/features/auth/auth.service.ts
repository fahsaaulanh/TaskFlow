import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Observable, catchError, of, switchMap, tap, throwError } from 'rxjs';

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
    return this.withCsrf(() => this.http.post<SessionUser>('/api/auth/login', { email, password })).pipe(
      tap((user) => this.session.set(user)),
    );
  }

  logout() {
    return this.withCsrf(() => this.http.post<void>('/api/auth/logout', null)).pipe(
      catchError((error: HttpErrorResponse) =>
        error.status === 401 ? of(undefined) : throwError(() => error),
      ),
      tap(() => this.session.set(null)),
    );
  }

  withCsrf<T>(request: () => Observable<T>) {
    return this.http.get<void>('/api/auth/csrf').pipe(switchMap(request));
  }
}
