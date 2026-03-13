import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../auth/auth.service';
import { Board } from './board';
import { BoardApi, TaskSummary } from './board-api';

describe('Board moves', () => {
  const first: TaskSummary = { id: 1, title: 'First task', description: null, status: 'Todo', priority: 'Medium',
    dueDate: null, assigneeId: null, sortOrder: 0, rowVersion: 'AQAAAAAAAAA=' };
  const second: TaskSummary = { ...first, id: 2, title: 'Second task', sortOrder: 1, rowVersion: 'AgAAAAAAAAA=' };
  const moveTask = vi.fn(() => of(first));

  beforeEach(() => {
    moveTask.mockClear();
    TestBed.configureTestingModule({
      imports: [Board],
      providers: [
        provideRouter([]),
        { provide: BoardApi, useValue: {
          list: () => of([{ id: 5, name: 'Team board' }]), members: () => of([]),
          tasks: () => of([first, second]), moveTask,
        } },
        { provide: AuthService, useValue: { user: signal(null), withCsrf: <T>(request: () => Observable<T>) => request() } },
      ],
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
});
