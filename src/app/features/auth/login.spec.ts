import { TestBed } from '@angular/core/testing';

import { Login } from './login';

describe('Login', () => {
  async function render(error?: string): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(Login);
    if (error !== undefined) {
      fixture.componentRef.setInput('error', error);
    }
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('links to the backend sign-in as a real navigation', async () => {
    const link = (await render()).querySelector('a');
    expect(link?.getAttribute('href')).toBe('/api/auth/google/login');
    expect(link?.textContent).toContain('Accedi con Google');
  });

  it('shows no message without an error', async () => {
    expect((await render()).querySelector('[role=alert]')).toBeNull();
  });

  it.each([
    ['cancelled', 'annullato'],
    ['failed', 'non riuscito'],
    ['unverified_email', 'non è verificato'],
    ['unavailable', 'non è disponibile'],
    ['something-else', 'non riuscito'],
  ])('explains the error %s', async (code, text) => {
    expect((await render(code)).querySelector('[role=alert]')?.textContent).toContain(text);
  });
});
