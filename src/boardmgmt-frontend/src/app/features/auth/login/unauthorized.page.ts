import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="container py-5 text-center">
      <h1 class="mb-3">Unauthorized</h1>
      <p>You don’t have permission to view this page.</p>
      <a routerLink="/dashboard" class="btn btn-primary mt-3">Go to Dashboard</a>
    </div>
  `,
})
export default class UnauthorizedPage {}
