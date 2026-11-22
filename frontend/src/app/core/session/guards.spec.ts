import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { provideFakeSession, USERS } from '../../../testing/fake-session';
import { adminGuard, landingPath } from './guards';
import { User } from './user';

@Component({ template: '' })
class Blank {}

describe('landingPath', () => {
  it.each([
    ['signed-out', null, '/login'],
    ['pending', USERS.pending, '/pending'],
    ['disabled', USERS.disabled, '/disabled'],
    ['active', USERS.active, '/'],
    ['admin', USERS.admin, '/'],
  ] as const)('sends a %s user to %s', (_label, user, path) => {
    expect(landingPath(user)).toBe(path);
  });
});

describe('adminGuard', () => {
  async function openAdminPage(user: User | null): Promise<string> {
    const [session] = provideFakeSession(user);
    TestBed.configureTestingModule({
      providers: [
        session,
        provideRouter([
          { path: 'admin', canActivate: [adminGuard], component: Blank },
          { path: '**', component: Blank },
        ]),
      ],
    });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/admin');
    return TestBed.inject(Router).url;
  }

  it.each([
    ['admin', USERS.admin, '/admin'],
    ['active', USERS.active, '/'],
    ['pending admin', { ...USERS.admin, status: 'pending' }, '/pending'],
    ['disabled admin', { ...USERS.admin, status: 'disabled' }, '/disabled'],
    ['signed-out', null, '/login'],
  ] as const)('sends a %s user to %s', async (_label, user, path) => {
    expect(await openAdminPage(user)).toBe(path);
  });
});
