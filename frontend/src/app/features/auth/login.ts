import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from './auth.service';

@Component({
  imports: [ReactiveFormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  protected readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email, Validators.maxLength(254)] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(1024)] }),
  });
  protected readonly busy = signal(false);
  protected readonly error = signal(this.route.snapshot.queryParamMap.has('unavailable')
    ? 'TaskFlow is temporarily unavailable. Try again in a moment.' : '');

  protected submit() {
    if (this.busy()) return;
    this.form.controls.email.setValue(this.form.controls.email.value.trim());
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    const { email, password } = this.form.getRawValue();
    this.error.set('');
    this.busy.set(true);
    this.auth.login(email, password).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.busy.set(false)),
    ).subscribe({
      next: () => void this.router.navigateByUrl('/board', { replaceUrl: true }),
      error: (error: HttpErrorResponse) => this.error.set(error.status === 401
        ? 'Invalid email or password.' : 'Could not sign in. Try again in a moment.'),
    });
  }
}
