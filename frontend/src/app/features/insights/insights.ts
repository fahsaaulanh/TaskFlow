import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, map, of, switchMap } from 'rxjs';
import { ActivityPage, BoardAnalytics, BoardApi, BoardSummary, TaskStatus } from '../board/board-api';

@Component({ imports: [DatePipe, RouterLink], templateUrl: './insights.html', styleUrl: './insights.css' })
export class Insights {
  private readonly api = inject(BoardApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly activityRequests = new Subject<{ boardId: number; from: string; to: string; page: number }>();
  private range = { from: '', to: '' };
  private requestedPage = 1;
  protected readonly board = signal<BoardSummary | null>(null);
  protected readonly analytics = signal<BoardAnalytics | null>(null);
  protected readonly activity = signal<ActivityPage | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadingActivity = signal(false);
  protected readonly error = signal('');
  protected readonly activityError = signal('');
  protected readonly rangeError = signal('');
  protected readonly statusLabels: Record<TaskStatus, string> = { Todo: 'To do', InProgress: 'In progress', Done: 'Done' };

  constructor() {
    this.activityRequests.pipe(
      switchMap(({ boardId, from, to, page }) => this.api.activity(boardId, from, to, page).pipe(
        catchError((error: HttpErrorResponse) => {
          this.loadingActivity.set(false);
          this.handleError(error, true);
          return EMPTY;
        }),
      )),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe((page) => {
      this.activity.set(page);
      this.loadingActivity.set(false);
    });
    this.load();
  }

  protected load() {
    this.loading.set(true);
    this.error.set('');
    this.api.list().pipe(
      switchMap(([board]) => board ? this.api.analytics(board.id).pipe(map((analytics) => ({ board, analytics }))) : of(null)),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: (result) => {
        this.loading.set(false);
        if (!result) return;
        this.board.set(result.board);
        this.analytics.set(result.analytics);
        this.loadActivity(1);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.handleError(error, false);
      },
    });
  }

  protected applyRange(from: string, to: string) {
    this.rangeError.set('');
    if (from && to && from > to) {
      this.rangeError.set('The end date must be on or after the start date.');
      return;
    }
    this.range = { from, to };
    this.loadActivity(1);
  }

  protected loadActivity(page = this.requestedPage) {
    const board = this.board();
    if (!board) return;
    this.requestedPage = page;
    this.loadingActivity.set(true);
    this.activityError.set('');
    this.activityRequests.next({ boardId: board.id, ...this.range, page });
  }

  private handleError(error: HttpErrorResponse, activity: boolean) {
    if (error.status === 401) {
      void this.router.navigateByUrl('/login', { replaceUrl: true });
      return;
    }
    const message = error.status === 403 ? 'You do not have access to this board.'
      : activity ? 'Could not load activity.' : 'Could not load insights.';
    (activity ? this.activityError : this.error).set(message);
  }
}
