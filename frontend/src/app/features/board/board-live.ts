import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

export type LiveStatus = 'connecting' | 'live' | 'reconnecting' | 'offline';

@Injectable()
export class BoardLive {
  private connection: HubConnection | null = null;
  private subscription: { boardId: number; refresh: () => void } | null = null;
  private readonly state = signal<LiveStatus>('connecting');
  readonly status = this.state.asReadonly();

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.subscription = null;
      const connection = this.connection;
      this.connection = null;
      void connection?.stop().catch(() => undefined);
    });
  }

  watch(boardId: number, refresh: () => void) {
    this.subscription = { boardId, refresh };
    void this.connect();
  }

  retry() {
    if (this.state() === 'offline') void this.connect();
  }

  private async connect() {
    const subscription = this.subscription;
    if (!subscription) return;
    this.state.set('connecting');
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/board')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    const previous = this.connection;
    this.connection = connection;
    await previous?.stop().catch(() => undefined);
    if (this.connection !== connection) return;

    connection.on('BoardChanged', () => {
      if (this.connection === connection) subscription.refresh();
    });
    connection.onreconnecting(() => {
      if (this.connection === connection) this.state.set('reconnecting');
    });
    connection.onreconnected(() => { void this.join(connection, subscription); });
    connection.onclose(() => {
      if (this.connection === connection) this.state.set('offline');
    });

    try {
      await connection.start();
      await this.join(connection, subscription);
    } catch {
      if (this.connection === connection) this.state.set('offline');
    }
  }

  private async join(connection: HubConnection, subscription: { boardId: number; refresh: () => void }) {
    try {
      await connection.invoke('WatchBoard', subscription.boardId);
      if (this.connection !== connection) return;
      this.state.set('live');
      // Reconcile after joining so changes made during a disconnect are included.
      subscription.refresh();
    } catch {
      if (this.connection === connection) this.state.set('offline');
      await connection.stop().catch(() => undefined);
    }
  }
}
