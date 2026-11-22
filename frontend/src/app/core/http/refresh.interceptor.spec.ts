import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { SessionService } from '../session/session.service';
import { CLOCK, LAST_REFRESH_KEY, REFRESH_URL } from '../session/token-refresher';
import { csrfInterceptor } from './csrf.interceptor';
import { refreshInterceptor } from './refresh.interceptor';

const UNAUTHORIZED = { status: 401, statusText: 'Unauthorized' };

/** Lets pending promises (the lock, the refresh) move on. */
async function settle(): Promise<void> {
  for (let i = 0; i < 10; i++) {
    await Promise.resolve();
  }
}

describe('refreshInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let session: SessionService;
  let time: number;

  beforeEach(() => {
    // Tests run without Web Locks: the in-tab lock is used
    vi.stubGlobal('navigator', {});
    localStorage.clear();
    time = 1000;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([refreshInterceptor, csrfInterceptor])),
        provideHttpClientTesting(),
        // Every reading of the clock is one millisecond later
        { provide: CLOCK, useValue: () => time++ },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionService);
    vi.spyOn(session, 'clear');
  });

  afterEach(() => {
    // Globals first: a failing verify() must not leak the stub into later tests
    vi.unstubAllGlobals();
    backend.verify();
  });

  it('passes successful answers through', async () => {
    const answer = firstValueFrom(http.get('/api/things'));
    backend.expectOne('/api/things').flush({ ok: true });
    await expect(answer).resolves.toEqual({ ok: true });
  });

  it('refreshes once and repeats the request after a 401', async () => {
    const answer = firstValueFrom(http.get('/api/things'));
    backend.expectOne('/api/things').flush(null, UNAUTHORIZED);
    await settle();

    const refresh = backend.expectOne(REFRESH_URL);
    expect(refresh.request.method).toBe('POST');
    expect(refresh.request.headers.get('X-Requested-With')).toBe('XMLHttpRequest');
    refresh.flush(null, { status: 204, statusText: 'No Content' });
    await settle();

    backend.expectOne('/api/things').flush({ ok: true });
    await expect(answer).resolves.toEqual({ ok: true });
    expect(Number(localStorage.getItem(LAST_REFRESH_KEY))).toBeGreaterThan(0);
  });

  it('shares one refresh between concurrent requests', async () => {
    const first = firstValueFrom(http.get('/api/one'));
    const second = firstValueFrom(http.get('/api/two'));
    backend.expectOne('/api/one').flush(null, UNAUTHORIZED);
    backend.expectOne('/api/two').flush(null, UNAUTHORIZED);
    await settle();

    backend.expectOne(REFRESH_URL).flush(null, { status: 204, statusText: 'No Content' });
    await settle();

    // The second request saw the new refresh time and did not refresh again
    backend.expectNone(REFRESH_URL);
    backend.expectOne('/api/one').flush('one');
    backend.expectOne('/api/two').flush('two');
    await expect(first).resolves.toBe('one');
    await expect(second).resolves.toBe('two');
  });

  it('does not refresh when another tab refreshed after the request was sent', async () => {
    const answer = firstValueFrom(http.get('/api/things'));
    // Another tab refreshes while this request is on its way
    localStorage.setItem(LAST_REFRESH_KEY, String(time + 100));
    backend.expectOne('/api/things').flush(null, UNAUTHORIZED);
    await settle();

    backend.expectNone(REFRESH_URL);
    backend.expectOne('/api/things').flush({ ok: true });
    await expect(answer).resolves.toEqual({ ok: true });
  });

  it('signs out when the refresh fails', async () => {
    const answer = firstValueFrom(http.get('/api/things'));
    backend.expectOne('/api/things').flush(null, UNAUTHORIZED);
    await settle();

    backend.expectOne(REFRESH_URL).flush({ detail: 'Session ended' }, UNAUTHORIZED);

    await expect(answer).rejects.toBeInstanceOf(HttpErrorResponse);
    expect(session.clear).toHaveBeenCalled();
    backend.expectNone('/api/things');
  });

  it('passes on a second 401 without refreshing again', async () => {
    const answer = firstValueFrom(http.get('/api/things'));
    backend.expectOne('/api/things').flush(null, UNAUTHORIZED);
    await settle();
    backend.expectOne(REFRESH_URL).flush(null, { status: 204, statusText: 'No Content' });
    await settle();

    backend.expectOne('/api/things').flush(null, UNAUTHORIZED);

    await expect(answer).rejects.toMatchObject({ status: 401 });
    await settle();
    backend.expectNone(REFRESH_URL);
  });

  it('refreshes for /api/auth/me, whose 401 may only mean an expired access token', async () => {
    const answer = firstValueFrom(http.get('/api/auth/me'));
    backend.expectOne('/api/auth/me').flush(null, UNAUTHORIZED);
    await settle();
    backend.expectOne(REFRESH_URL).flush(null, { status: 204, statusText: 'No Content' });
    await settle();
    backend.expectOne('/api/auth/me').flush({ id: 'u1' });
    await expect(answer).resolves.toEqual({ id: 'u1' });
  });

  it.each([
    ['POST', REFRESH_URL],
    ['POST', '/api/auth/logout'],
    ['GET', '/api/auth/google/login'],
  ])('never refreshes for %s %s', async (method, url) => {
    const answer = firstValueFrom(http.request(method, url));
    backend.expectOne(url).flush(null, UNAUTHORIZED);

    await expect(answer).rejects.toMatchObject({ status: 401 });
    await settle();
    backend.expectNone(REFRESH_URL);
  });

  it('passes other errors through without refreshing', async () => {
    const answer = firstValueFrom(http.get('/api/things'));
    backend.expectOne('/api/things').flush(null, { status: 403, statusText: 'Forbidden' });

    await expect(answer).rejects.toMatchObject({ status: 403 });
    await settle();
    backend.expectNone(REFRESH_URL);
  });
});
