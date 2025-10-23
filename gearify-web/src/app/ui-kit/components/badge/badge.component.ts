import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type BadgeVariant = 'primary' | 'success' | 'warning' | 'danger' | 'info';
export type BadgeSize = 'small' | 'medium' | 'large';

/**
 * Badge component for labels, tags, and status indicators
 */
@Component({
  selector: 'app-badge',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span [class]="'badge badge-' + variant + ' badge-' + size" [class.badge-pill]="pill">
      <ng-content></ng-content>
    </span>
  `,
  styleUrl: './badge.component.scss'
})
export class BadgeComponent {
  @Input() variant: BadgeVariant = 'primary';
  @Input() size: BadgeSize = 'medium';
  @Input() pill = false;
}
