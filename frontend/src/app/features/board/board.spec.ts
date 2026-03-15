import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../auth/auth.service';
import { Board } from './board';
import { BoardApi, TaskSummary } from './board-api';
import { BoardLive } from './board-live';

describe('Board moves', () => {
  const first: TaskSummary = { id: 1, title: 'First task', description: null, status: 'Todo', priority: 'Medium',
    dueDate: null, assigneeId: null, sortOrder: 0, rowVersion: 'AQAAAAAAAAA=' };
  const second: TaskSummary = { ...first, id: 2, title: 'Second task', sortOrder: 1, rowVersion: 'AgAAAAAAAAA=' };
  const moveTask = vi.fn(() => of(first));
  const listTasks = vi.fn(() => of([first, second]));
  const watch = vi.fn<(boardId: number, refresh: () => void) => void>();

  beforeEach(() => {
    moveTask.mockClear();
    listTasks.mockClear();
    watch.mockClear();
    TestBed.configureTestingModule({
      imports: [Board],
      providers: [
        provideRouter([]),
        { provide: BoardApi, useValue: {
          list: () => of([{ id: 5, name: 'Team board' }]), members: () => of([]),
          tasks: listTasks, moveTask,
        } },
        { provide: AuthService, useValue: { user: signal(null), withCsrf: <T>(request: () => Observable<T>) => request() } },
      ],
    }).overrideComponent(Board, {
      set: { providers: [{ provide: BoardLive, useValue: { status: signal('live'), watch } }] },
    });
  });

  it('sends one move anchored before the card that receives the drop', () => {
    const fixture = TestBed.createComponent(Board);
    fixture.detectChanges();
    const [firstCard, secondCard] = (fixture.nativeElement as HTMLElement).querySelectorAll('article');
    const dragStart = new Event('dragstart', { bubbles: true, cancelable: true });
    Object.defineProperty(dragStart, 'dataTransfer', { value: { effectAllowed: '', setData: vi.fn() } });
    secondCard.dispatchEvent(dragStart);
    firstCard.dispatchEvent(new Event('drop', { bubbles: true, cancelable: true }));

    expect(moveTask).toHaveBeenCalledTimes(1);
    expect(moveTask).toHaveBeenCalledWith(5, second, 'Todo', first.id);
  });

  it('moves to the end of the chosen status using the card menu', () => {
    const fixture = TestBed.createComponent(Board);
    fixture.detectChanges();
    const select = (fixture.nativeElement as HTMLElement).querySelector<HTMLSelectElement>('article select')!;
    select.value = 'Done';
    select.dispatchEvent(new Event('change', { bubbles: true }));

    expect(moveTask).toHaveBeenCalledWith(5, first, 'Done', null);
  });

  it('reloads the selected board when the live subscription requests reconciliation', () => {
    const fixture = TestBed.createComponent(Board);
    fixture.detectChanges();
    const [boardId, refresh] = watch.mock.calls[0];
    expect(boardId).toBe(5);
    expect(listTasks).toHaveBeenCalledTimes(1);

    refresh();

    expect(listTasks).toHaveBeenCalledTimes(2);
    expect(listTasks).toHaveBeenLastCalledWith(5, { status: '', priority: '', assigneeId: '' });
  });
});
