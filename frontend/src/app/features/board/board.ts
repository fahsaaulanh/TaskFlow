import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { EMPTY, Subject, catchError, finalize, map, of, switchMap } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { BoardApi, BoardMember, BoardSummary, TaskFilters, TaskStatus, TaskSummary } from './board-api';
import { TaskEditor } from './task-editor';

const columns: ReadonlyArray<{ status: TaskStatus; label: string }> = [
  { status: 'Todo', label: 'To do' },
  { status: 'InProgress', label: 'In progress' },
  { status: 'Done', label: 'Done' },
];
const emptyFilters: TaskFilters = { status: '', priority: '', assigneeId: '' };

@Component({ imports: [DatePipe, TaskEditor], templateUrl: './board.html', styleUrl: './board.css' })
export class Board {
  private readonly api = inject(BoardApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly taskRequests = new Subject<{ boardId: number; filters: TaskFilters }>();
  private readonly tasks = signal<TaskSummary[]>([]);
  protected readonly board = signal<BoardSummary | null>(null);
  protected readonly members = signal<BoardMember[]>([]);
  protected readonly filters = signal<TaskFilters>(emptyFilters);
  protected readonly user = this.auth.user;
  protected readonly loading = signal(true);
  protected readonly error = signal('');
  protected readonly logoutError = signal('');
  protected readonly updateMessage = signal('');
  protected readonly signingOut = signal(false);
  protected readonly statuses = columns;
  protected readonly total = computed(() => this.tasks().length);
  protected readonly lanes = computed(() => columns.map((column) => ({
    ...column, tasks: this.tasks().filter((task) => task.status === column.status),
  })));
  protected readonly memberNames = computed(() => new Map(this.members().map((member) => [member.id, member.displayName])));

  constructor() {
    this.taskRequests.pipe(
      switchMap(({ boardId, filters }) => this.api.tasks(boardId, filters).pipe(
        catchError((error: HttpErrorResponse) => {
          this.handleLoadError(error);
          return EMPTY;
        }),
      )),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe((tasks) => {
      this.tasks.set(tasks);
      this.loading.set(false);
    });
    this.loadBoard();
  }

  protected changeFilter(field: keyof TaskFilters, value: string) {
    this.filters.update((filters) => ({ ...filters, [field]: value }));
    this.loadTasks();
  }

  protected clearFilters() {
    this.filters.set(emptyFilters);
    this.loadTasks();
  }

  protected retry() {
    if (this.board()) this.loadTasks();
    else this.loadBoard();
  }

  protected taskSaved(message: string) {
    this.updateMessage.set(message);
    this.loadTasks();
  }

  protected sessionExpired() {
    void this.router.navigateByUrl('/login', { replaceUrl: true });
  }

  protected signOut() {
    if (this.signingOut()) return;
    this.logoutError.set('');
    this.signingOut.set(true);
    this.auth.logout().pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.signingOut.set(false)),
    ).subscribe({
      next: () => void this.router.navigateByUrl('/login', { replaceUrl: true }),
      error: () => this.logoutError.set('Could not sign out. Try again.'),
    });
  }

  private loadBoard() {
    this.loading.set(true);
    this.error.set('');
    this.api.list().pipe(
      switchMap(([board]) => board
        ? this.api.members(board.id).pipe(map((members) => ({ board, members })))
        : of(null)),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: (result) => {
        if (!result) {
          this.loading.set(false);
          return;
        }
        this.board.set(result.board);
        this.members.set(result.members);
        this.loadTasks();
      },
      error: (error: HttpErrorResponse) => this.handleLoadError(error),
    });
  }

  private loadTasks() {
    const board = this.board();
    if (!board) return;
    this.loading.set(true);
    this.error.set('');
    this.taskRequests.next({ boardId: board.id, filters: this.filters() });
  }

  private handleLoadError(error: HttpErrorResponse) {
    this.loading.set(false);
    if (error.status === 401) {
      this.sessionExpired();
    } else {
      this.error.set(error.status === 403 ? 'You do not have access to this board.' : 'Could not load the board.');
    }
  }
}
