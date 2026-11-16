import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { CSRF_HEADER, csrfInterceptor } from './csrf.interceptor';

describe('csrfInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([csrfInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it.each(['POST', 'PUT', 'PATCH', 'DELETE'])(
    'adds the header to %s requests to the API',
    (method) => {
      http.request(method, '/api/admin/users/1/approve').subscribe();
      const req = backend.expectOne('/api/admin/users/1/approve');
      expect(req.request.headers.get(CSRF_HEADER)).toBe('XMLHttpRequest');
      req.flush(null);
    },
  );

  it('leaves GET requests unchanged', () => {
    http.get('/api/auth/me').subscribe();
    const req = backend.expectOne('/api/auth/me');
    expect(req.request.headers.has(CSRF_HEADER)).toBe(false);
    req.flush(null);
  });

  it('never sends the header to other sites', () => {
    http.post('https://example.com/api/x', null).subscribe();
    const req = backend.expectOne('https://example.com/api/x');
    expect(req.request.headers.has(CSRF_HEADER)).toBe(false);
    req.flush(null);
  });
});
