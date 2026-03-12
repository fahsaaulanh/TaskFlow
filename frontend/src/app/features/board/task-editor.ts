import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, inject, input, output, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, Observable } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { BoardApi, BoardMember, TaskPriority, TaskSummary } from './board-api';

@Component({
  selector: 'app-task-editor',
  imports: [ReactiveFormsModule],
  templateUrl: './task-editor.html',
  styleUrl: './task-editor.css',
})
export class TaskEditor {
  private readonly api = inject(BoardApi);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private boardId = 0;
  readonly members = input.required<BoardMember[]>();
  readonly saved = output<string>();
  readonly refresh = output<void>();
  readonly sessionExpired = output<void>();
  protected readonly task = signal<TaskSummary | null>(null);
  protected readonly busy = signal(false);
  protected readonly conflict = signal(false);
  protected readonly confirmingArchive = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly form = new FormGroup({
    title: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(3), Validators.maxLength(120)] }),
    description: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    priority: new FormControl<TaskPriority>('Medium', { nonNullable: true }),
    dueDate: new FormControl('', { nonNullable: true }),
    assigneeId: new FormControl<number | null>(null),
  });

  open(boardId: number, task: TaskSummary | null = null) {
    this.boardId = boardId;
    this.task.set(task);
    this.conflict.set(false);
    this.confirmingArchive.set(false);
    this.errors.set([]);
    this.form.reset({ title: task?.title ?? '', description: task?.description ?? '', priority: task?.priority ?? 'Medium',
      dueDate: task?.dueDate ?? '', assigneeId: task?.assigneeId ?? null });
    this.dialog().nativeElement.showModal();
  }

  protected save() {
    if (this.busy() || this.conflict() || this.confirmingArchive()) return;
    this.form.controls.title.setValue(this.form.controls.title.value.trim());
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    const values = this.form.getRawValue();
    const form = { ...values, dueDate: values.dueDate || null, description: values.description.trim() || null };
    const task = this.task();
    this.send(() => task ? this.api.editTask(this.boardId, task, form) : this.api.createTask(this.boardId, form),
      task ? 'Task updated.' : 'Task created.');
  }

  protected archive() {
    const task = this.task();
    if (!task || this.busy() || this.conflict()) return;
    this.send(() => this.api.archiveTask(this.boardId, task), 'Task archived.');
  }

  protected close() { this.dialog().nativeElement.close(); }

  protected afterClose() {
    if (this.conflict()) this.refresh.emit();
  }

  protected cancel(event: Event) {
    if (this.busy()) event.preventDefault();
  }

  private send<T>(request: () => Observable<T>, message: string) {
    this.errors.set([]);
    this.busy.set(true);
    this.auth.withCsrf(request).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.busy.set(false)),
    ).subscribe({
      next: () => {
        this.close();
        this.saved.emit(message);
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 401) {
          this.close();
          this.sessionExpired.emit();
        } else if (error.status === 409 || error.status === 404) {
          this.conflict.set(true);
          this.errors.set(['This task changed or was archived. Close the editor to reload the board.']);
        } else {
          const fields = error.error?.errors as Record<string, string[]> | undefined;
          this.errors.set(fields ? Object.values(fields).flat() : ['Could not save the change. Try again.']);
        }
      },
    });
  }
}
