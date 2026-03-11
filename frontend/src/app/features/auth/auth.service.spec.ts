import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;
  const user = { id: 1, email: 'alex@taskflow.test', displayName: 'Alex' };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests a fresh CSRF token before each authentication change', async () => {
    const login = firstValueFrom(auth.login(user.email, 'test-only-password'));
    http.expectNone('/api/auth/login');
    http.expectOne({ method: 'GET', url: '/api/auth/csrf' }).flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne({ method: 'POST', url: '/api/auth/login' }).flush(user);
    await login;
    expect(auth.user()).toEqual(user);

    const logout = firstValueFrom(auth.logout());
    http.expectNone('/api/auth/logout');
    http.expectOne({ method: 'GET', url: '/api/auth/csrf' }).flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne({ method: 'POST', url: '/api/auth/logout' }).flush(null, { status: 204, statusText: 'No Content' });
    await logout;
    expect(auth.user()).toBeNull();
  });

  it('restores an active session and clears it when the API returns 401', async () => {
    const restored = firstValueFrom(auth.restore());
    http.expectOne('/api/auth/session').flush(user);
    expect(await restored).toEqual(user);
    expect(auth.user()).toEqual(user);

    const expired = firstValueFrom(auth.restore());
    http.expectOne('/api/auth/session').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(await expired).toBeNull();
    expect(auth.user()).toBeNull();
  });
});
