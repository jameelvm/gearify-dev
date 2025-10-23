import { Component, Input, Output, EventEmitter, OnInit, OnDestroy, HostListener, effect, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { trigger, state, style, transition, animate } from '@angular/animations';

export type ModalSize = 'small' | 'medium' | 'large' | 'full-screen';

/**
 * Reusable modal/dialog component with multiple sizes and animations
 *
 * @example
 * <app-modal
 *   [size]="'medium'"
 *   [title]="'Confirm Action'"
 *   [closeOnBackdrop]="true"
 *   (closed)="handleClose()">
 *   <div header>Custom Header</div>
 *   <div body>Modal content goes here</div>
 *   <div footer>
 *     <app-button (clicked)="handleConfirm()">Confirm</app-button>
 *   </div>
 * </app-modal>
 */
@Component({
  selector: 'app-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './modal.component.html',
  styleUrl: './modal.component.scss',
  animations: [
    trigger('slideIn', [
      state('void', style({
        transform: 'translateY(-50px)',
        opacity: 0
      })),
      state('*', style({
        transform: 'translateY(0)',
        opacity: 1
      })),
      transition('void => *', animate('300ms ease-out')),
      transition('* => void', animate('200ms ease-in'))
    ]),
    trigger('fadeIn', [
      state('void', style({
        opacity: 0
      })),
      state('*', style({
        opacity: 1
      })),
      transition('void => *', animate('200ms ease-out')),
      transition('* => void', animate('150ms ease-in'))
    ])
  ]
})
export class ModalComponent implements OnInit, OnDestroy {
  @Input() size: ModalSize = 'medium';
  @Input() closeOnBackdrop = true;
  @Input() title?: string;
  @Input() showCloseButton = true;

  @Output() closed = new EventEmitter<void>();

  // Signal for reactive state management
  isVisible = signal(false);

  constructor() {
    // Effect to handle body scroll lock
    effect(() => {
      if (this.isVisible()) {
        document.body.style.overflow = 'hidden';
      } else {
        document.body.style.overflow = '';
      }
    });
  }

  ngOnInit(): void {
    // Trigger animation after component is initialized
    setTimeout(() => this.isVisible.set(true), 10);
  }

  ngOnDestroy(): void {
    // Ensure body scroll is restored
    document.body.style.overflow = '';
  }

  /**
   * Handle ESC key press to close modal
   */
  @HostListener('document:keydown.escape', ['$event'])
  handleEscapeKey(event: KeyboardEvent): void {
    event.preventDefault();
    this.close();
  }

  /**
   * Handle backdrop click
   */
  onBackdropClick(event: MouseEvent): void {
    if (this.closeOnBackdrop && event.target === event.currentTarget) {
      this.close();
    }
  }

  /**
   * Close the modal and emit closed event
   */
  close(): void {
    this.isVisible.set(false);
    // Wait for animation to complete before emitting
    setTimeout(() => {
      this.closed.emit();
    }, 200);
  }

  /**
   * Get CSS classes for modal based on size
   */
  getModalClasses(): string {
    return `modal-content modal-${this.size}`;
  }
}
