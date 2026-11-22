import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { FakeSession, provideFakeSession, USERS } from '../../../testing/fake-session';
import { routes } from '../../app.routes';
import { Confirm } from '../../core/confirm';
import { User } from '../../core/session/user';
import { AdminUser } from './admin-users.service';

function adminView(user: User): AdminUser {
  return {
    ...user,
    created_at: '2026-11-01T10:00:00Z',
    last_login_at: null,
    approved_at: null,
    approved_by: null,
  };
}

const ME = adminView(USERS.admin);
const PENDING = adminView(USERS.pending);
const ACTIVE = adminView(USERS.active);

/** Lets pending promises and the resulting change detection move on. */
async function settle(harness: RouterTestingHarness): Promise<void> {
  for (let i = 0; i < 5; i++) {
    await Promise.resolve();
  }
  await harness.fixture.whenStable();
}

describe('AdminUsers', () => {
  let backend: HttpTestingController;
  let session: FakeSession;
  let confirmAnswer: boolean;
  let questions: string[];
  let harness: RouterTestingHarness;

  beforeEach(() => {
    confirmAnswer = true;
    questions = [];
    const [sessionProvider, fake] = provideFakeSession(USERS.admin);
    session = fake;
    TestBed.configureTestingModule({
      providers: [
        sessionProvider,
        provideRouter(routes, withComponentInputBinding()),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: Confirm,
          useValue: {
            ask: (question: string) => {
              questions.push(question);
              return confirmAnswer;
            },
          },
        },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  async function open(url: string, users: AdminUser[]): Promise<HTMLElement> {
    harness = await RouterTestingHarness.create();
    const navigation = harness.navigateByUrl(url);
    await settle(harness);
    backend.expectOne((req) => req.url === '/api/admin/users').flush(users);
    await navigation;
    await settle(harness);
    return harness.routeNativeElement as HTMLElement;
  }

  function button(page: HTMLElement, rowText: string, label: string): HTMLButtonElement {
    const row = [...page.querySelectorAll('li')].find((li) => li.textContent?.includes(rowText));
    const found = [...(row?.querySelectorAll('button') ?? [])].find((b) =>
      b.textContent?.includes(label),
    );
    if (!found) {
      throw new Error(`No "${label}" button for ${rowText}`);
    }
    return found;
  }

  it('lists users with the status filter from the address', async () => {
    harness = await RouterTestingHarness.create();
    void harness.navigateByUrl('/admin/users?status=pending');
    await settle(harness);
    const req = backend.expectOne((r) => r.url === '/api/admin/users');
    expect(req.request.params.get('status')).toBe('pending');
    req.flush([PENDING]);
    await settle(harness);

    const page = harness.routeNativeElement as HTMLElement;
    expect(page.textContent).toContain('new@example.com');
    expect(page.querySelector('[aria-current=page]')?.textContent).toContain('In attesa');
  });

  it('lists every user without a filter', async () => {
    harness = await RouterTestingHarness.create();
    void harness.navigateByUrl('/admin/users');
    await settle(harness);
    const req = backend.expectOne((r) => r.url === '/api/admin/users');
    expect(req.request.params.has('status')).toBe(false);
    req.flush([ME, ACTIVE]);
  });

  it('approves a pending user, who then leaves the pending list', async () => {
    const page = await open('/admin/users?status=pending', [PENDING]);

    button(page, 'new@example.com', 'Approva').click();
    await settle(harness);
    const req = backend.expectOne(`/api/admin/users/${PENDING.id}/approve`);
    expect(req.request.method).toBe('POST');
    req.flush({ ...PENDING, status: 'active' });
    await settle(harness);

    expect(page.textContent).not.toContain('new@example.com');
    expect(page.textContent).toContain('Nessun utente');
  });

  it('asks before disabling, and does nothing when the answer is no', async () => {
    const page = await open('/admin/users', [ME, ACTIVE]);
    confirmAnswer = false;

    button(page, 'ada@example.com', 'Disattiva').click();
    await settle(harness);

    expect(questions[0]).toContain('Disattivare Ada Example?');
    backend.expectNone(`/api/admin/users/${ACTIVE.id}/disable`);
  });

  it('disables after confirmation and shows the new status', async () => {
    const page = await open('/admin/users', [ME, ACTIVE]);

    button(page, 'ada@example.com', 'Disattiva').click();
    await settle(harness);
    backend
      .expectOne(`/api/admin/users/${ACTIVE.id}/disable`)
      .flush({ ...ACTIVE, status: 'disabled' });
    await settle(harness);

    expect(button(page, 'ada@example.com', 'Riattiva')).toBeTruthy();
  });

  it('promotes a user without asking', async () => {
    const page = await open('/admin/users', [ME, ACTIVE]);

    button(page, 'ada@example.com', 'Rendi amministratore').click();
    await settle(harness);
    const req = backend.expectOne(`/api/admin/users/${ACTIVE.id}/role`);
    expect(req.request.body).toEqual({ role: 'admin' });
    req.flush({ ...ACTIVE, role: 'admin' });
    await settle(harness);

    expect(questions).toEqual([]);
    expect(button(page, 'ada@example.com', 'Togli amministratore')).toBeTruthy();
  });

  it('shows the last-admin refusal from the backend', async () => {
    const page = await open('/admin/users', [ME]);

    button(page, 'admin@example.com', 'Togli amministratore').click();
    await settle(harness);
    backend
      .expectOne(`/api/admin/users/${ME.id}/role`)
      .flush(
        { detail: 'the last active administrator cannot be removed' },
        { status: 409, statusText: 'Conflict' },
      );
    await settle(harness);

    expect(questions[0]).toContain('rinunciare al ruolo');
    expect(page.querySelector('[role=alert]')?.textContent).toContain(
      'ultimo amministratore attivo',
    );
    expect(session.loadCalls).toBe(0);
  });

  it('leaves the page after removing my own admin role', async () => {
    const page = await open('/admin/users', [
      ME,
      adminView({
        ...USERS.admin,
        id: '00000000-0000-4000-8000-000000000009',
        email: 'other@example.com',
      }),
    ]);
    session.nextUser = { ...USERS.admin, role: 'user' };

    button(page, '(tu)', 'Togli amministratore').click();
    await settle(harness);
    backend.expectOne(`/api/admin/users/${ME.id}/role`).flush({ ...ME, role: 'user' });
    await settle(harness);
    await settle(harness);

    expect(session.loadCalls).toBe(1);
    expect(TestBed.inject(Router).url).toBe('/');
  });
});
