import { TestBed } from '@angular/core/testing';

import { provideFakeSession, USERS } from '../../testing/fake-session';
import { LogoutButton } from './logout-button';

describe('LogoutButton', () => {
  it('signs out when pressed', async () => {
    const [session, fake] = provideFakeSession(USERS.active);
    TestBed.configureTestingModule({ providers: [session] });
    const fixture = TestBed.createComponent(LogoutButton);
    await fixture.whenStable();

    (fixture.nativeElement as HTMLElement).querySelector('button')?.click();
    await fixture.whenStable();

    expect(fake.logoutCalls).toBe(1);
    expect(fake.user()).toBeNull();
  });
});
