import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { User } from './core/session/user';
import { routes } from './app.routes';
import { provideFakeSession, USERS } from '../testing/fake-session';

/** Opens `url` as `user` and returns where the router ended up, and the page. */
async function open(user: User | null, url: string): Promise<{ url: string; page: HTMLElement }> {
  const [session] = provideFakeSession(user);
  TestBed.configureTestingModule({
    providers: [session, provideRouter(routes, withComponentInputBinding())],
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url);
  return { url: TestBed.inject(Router).url, page: harness.routeNativeElement as HTMLElement };
}

describe('routes', () => {
  it.each([
    // who, where they go, where they end up
    ['signed-out', null, '/', '/login'],
    ['signed-out', null, '/pending', '/login'],
    ['signed-out', null, '/login', '/login'],
    ['pending', USERS.pending, '/', '/pending'],
    ['pending', USERS.pending, '/login', '/pending'],
    ['pending', USERS.pending, '/disabled', '/pending'],
    ['disabled', USERS.disabled, '/', '/disabled'],
    ['disabled', USERS.disabled, '/pending', '/disabled'],
    ['active', USERS.active, '/login', '/'],
    ['active', USERS.active, '/pending', '/'],
    ['active', USERS.active, '/', '/'],
    ['active', USERS.active, '/no-such-page', '/'],
  ] as const)('sends a %s user from %s to %s', async (_label, user, from, to) => {
    expect((await open(user, from)).url).toBe(to);
  });

  it('keeps the error code on the login page', async () => {
    const { url, page } = await open(null, '/login?error=cancelled');
    expect(url).toBe('/login?error=cancelled');
    expect(page.querySelector('[role=alert]')?.textContent).toContain('annullato');
  });

  it('shows the pending page with the account address', async () => {
    const { page } = await open(USERS.pending, '/pending');
    expect(page.querySelector('h1')?.textContent).toContain('in attesa di approvazione');
    expect(page.textContent).toContain('new@example.com');
  });

  it('shows the disabled page', async () => {
    const { page } = await open(USERS.disabled, '/disabled');
    expect(page.querySelector('h1')?.textContent).toContain('disattivato');
  });

  it('greets an active user by first name inside the header frame', async () => {
    const { page } = await open(USERS.active, '/');
    expect(page.querySelector('header')?.textContent).toContain('Ada Example');
    expect(page.querySelector('h1')?.textContent).toContain('Ciao, Ada');
  });
});
