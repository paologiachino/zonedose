import { TestBed } from '@angular/core/testing';

import { RefreshLock } from './refresh-lock';

describe('RefreshLock', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('uses the Web Locks API when the browser has it', async () => {
    const request = vi.fn((_name: string, task: () => Promise<string>) => task());
    vi.stubGlobal('navigator', { locks: { request } });

    const result = await TestBed.inject(RefreshLock).runExclusive(async () => 'done');

    expect(result).toBe('done');
    expect(request).toHaveBeenCalledWith('sportlab-refresh', expect.any(Function));
  });

  describe('without the Web Locks API', () => {
    beforeEach(() => vi.stubGlobal('navigator', {}));

    it('runs one task at a time, in order', async () => {
      const lock = TestBed.inject(RefreshLock);
      const events: string[] = [];
      let release!: () => void;
      const first = lock.runExclusive(async () => {
        events.push('first start');
        await new Promise<void>((resolve) => (release = resolve));
        events.push('first end');
      });
      const second = lock.runExclusive(async () => {
        events.push('second');
      });

      await Promise.resolve();
      await Promise.resolve();
      expect(events).toEqual(['first start']);
      release();
      await Promise.all([first, second]);
      expect(events).toEqual(['first start', 'first end', 'second']);
    });

    it('keeps going after a failed task', async () => {
      const lock = TestBed.inject(RefreshLock);
      const failed = lock.runExclusive(() => Promise.reject(new Error('refresh failed')));
      const next = lock.runExclusive(async () => 'next');

      await expect(failed).rejects.toThrow('refresh failed');
      await expect(next).resolves.toBe('next');
    });
  });
});
