import { TestBed } from '@angular/core/testing';

import { Home } from './home';

describe('Home', () => {
  it('shows the site name and the placeholder message', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('h1')?.textContent).toContain('Sportlab');
    expect(element.textContent).toContain('in costruzione');
  });
});
