import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { SessionService } from './session.service';
import { User } from './user';

const ACTIVE_USER: User = {
  id: '0b6c1f2e-0000-4000-8000-000000000001',
  email: 'ada@example.com',
  display_name: 'Ada Example',
  status: 'active',
  role: 'user',
};

describe('SessionService', () => {
  let session: SessionService;
  let backend: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    session = TestBed.inject(SessionService);
    backend = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => backend.verify());

  async function loadWith(user: User): Promise<void> {
    const loading = session.load();
    backend.expectOne('/api/auth/me').flush(user);
    await loading;
  }

  it('starts signed out', () => {
    expect(session.user()).toBeNull();
    expect(session.isActive()).toBe(false);
  });

  it('loads the signed-in user', async () => {
    await loadWith(ACTIVE_USER);
    expect(session.user()).toEqual(ACTIVE_USER);
    expect(session.isActive()).toBe(true);
    expect(session.isAdmin()).toBe(false);
  });

  it('recognises an active administrator', async () => {
    await loadWith({ ...ACTIVE_USER, role: 'admin' });
    expect(session.isAdmin()).toBe(true);
  });

  it('does not treat a pending administrator as active or admin', async () => {
    await loadWith({ ...ACTIVE_USER, role: 'admin', status: 'pending' });
    expect(session.isActive()).toBe(false);
    expect(session.isAdmin()).toBe(false);
  });

  it.each([
    [401, 'Unauthorized'],
    [502, 'Bad Gateway'],
  ])('stays signed out when /me answers %i', async (status, statusText) => {
    const loading = session.load();
    backend.expectOne('/api/auth/me').flush(null, { status, statusText });
    await loading;
    expect(session.user()).toBeNull();
  });

  it('logs out on the backend, forgets the user and goes to the login page', async () => {
    await loadWith(ACTIVE_USER);
    const out = session.logout();
    const req = backend.expectOne('/api/auth/logout');
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 204, statusText: 'No Content' });
    await out;
    expect(session.user()).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('signs out locally even when the logout request fails', async () => {
    await loadWith(ACTIVE_USER);
    const out = session.logout();
    backend.expectOne('/api/auth/logout').flush(null, { status: 502, statusText: 'Bad Gateway' });
    await out;
    expect(session.user()).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('sends a signed-in user to the login page when the session ends', async () => {
    await loadWith(ACTIVE_USER);
    session.clear();
    expect(session.user()).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('does not navigate when nobody was signed in', () => {
    session.clear();
    expect(navigate).not.toHaveBeenCalled();
  });
});
