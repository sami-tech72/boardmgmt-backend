import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '@core/services/auth.service';
import { MeService } from '@core/services/me.service';
import { AccessService } from '@core/services/access.service';

@Component({
  standalone: true,
  selector: 'app-login',
  imports: [CommonModule, FormsModule],
  templateUrl: './login.page.html',
  styleUrls: ['./login.page.scss'],
})
export class LoginPage {
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private me = inject(MeService);
  private access = inject(AccessService);

  email = '';
  password = '';
  rememberMe = false;
  showPassword = false;
  loading = false;

  sendingReset = false;
  resetEmail = '';

  togglePassword() { this.showPassword = !this.showPassword; }

  private touchAll(form: NgForm) {
    Object.values(form.controls).forEach((ctrl) => {
      ctrl.markAsTouched({ onlySelf: true });
      ctrl.updateValueAndValidity({ onlySelf: true });
    });
  }

  onSubmit(form: NgForm) {
    if (this.loading) return;
    if (form.invalid) { this.touchAll(form); return; }

    this.loading = true;
    this.auth.login(this.email, this.password).subscribe({
      next: async ({ token }) => {
        this.auth.setToken(token);
        try {
          // ensure permissions for this just-logged-in user
          await this.me.ensureLoaded();
        } finally {
          this.loading = false;
        }

        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        if (returnUrl && this.access.canAccessPath(returnUrl)) {
          this.router.navigateByUrl(returnUrl);
          return;
        }

        const first = this.access.firstAllowedPath();
        this.router.navigateByUrl(first ?? '/dashboard');
      },
      error: (err) => {
        this.loading = false;
        alert(err?.message ?? 'Invalid credentials');
      },
    });
  }

  sendResetLink(form: NgForm) {
    if (this.sendingReset) return;
    this.sendingReset = true;

    // TODO: replace with real API call
    setTimeout(() => {
      this.sendingReset = false;
      const el = document.getElementById('forgotPasswordModal');
      // @ts-ignore bootstrap if present
      const modal = (window as any).bootstrap?.Modal?.getOrCreateInstance?.(el);
      modal?.hide?.();
      alert('If your email exists, you will receive a reset link.');
      form.reset();
    }, 600);
  }
}
