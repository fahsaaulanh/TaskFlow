import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export type TaskStatus = 'Todo' | 'InProgress' | 'Done';
export type TaskPriority = 'Low' | 'Medium' | 'High';

export interface BoardSummary { id: number; name: string; }
export interface BoardMember { id: number; displayName: string; }
export interface TaskSummary {
  id: number;
  title: string;
  description: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  dueDate: string | null;
  assigneeId: number | null;
  sortOrder: number;
  rowVersion: string;
}

export interface TaskFilters { status: string; priority: string; assigneeId: string; }
export interface TaskForm {
  title: string;
  description: string | null;
  priority: TaskPriority;
  dueDate: string | null;
  assigneeId: number | null;
}

@Injectable({ providedIn: 'root' })
export class BoardApi {
  private readonly http = inject(HttpClient);

  list() { return this.http.get<BoardSummary[]>('/api/boards'); }

  members(boardId: number) {
    return this.http.get<BoardMember[]>(`/api/boards/${boardId}/members`);
  }

  tasks(boardId: number, filters: TaskFilters) {
    let params = new HttpParams();
    for (const [name, value] of Object.entries(filters)) {
      if (value) params = params.set(name, value);
    }
    return this.http.get<TaskSummary[]>(`/api/boards/${boardId}/tasks`, { params });
  }

  createTask(boardId: number, form: TaskForm) {
    return this.http.post<TaskSummary>(`/api/boards/${boardId}/tasks`, form);
  }

  editTask(boardId: number, task: TaskSummary, form: TaskForm) {
    return this.http.put<TaskSummary>(`/api/boards/${boardId}/tasks/${task.id}`, { ...form, rowVersion: task.rowVersion });
  }

  archiveTask(boardId: number, task: TaskSummary) {
    return this.http.post<void>(`/api/boards/${boardId}/tasks/${task.id}/archive`, { rowVersion: task.rowVersion });
  }
}
