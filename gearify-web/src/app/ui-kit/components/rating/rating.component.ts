import { Component, Input, Output, EventEmitter, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Star rating component with read-only and interactive modes
 *
 * @example
 * <!-- Read-only rating with count -->
 * <app-rating [rating]="4.5" [count]="127"></app-rating>
 *
 * @example
 * <!-- Interactive rating -->
 * <app-rating [rating]="0" [interactive]="true" (ratingChanged)="handleRating($event)"></app-rating>
 */
@Component({
  selector: 'app-rating',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './rating.component.html',
  styleUrl: './rating.component.scss'
})
export class RatingComponent {
  @Input() set rating(value: number) {
    this.currentRating.set(this.clampRating(value));
  }

  @Input() maxRating = 5;
  @Input() interactive = false;
  @Input() count?: number;

  @Output() ratingChanged = new EventEmitter<number>();

  // Reactive state using signals
  currentRating = signal<number>(0);
  hoveredRating = signal<number>(0);

  // Computed value for display rating (hover overrides current in interactive mode)
  displayRating = computed(() => {
    if (this.interactive && this.hoveredRating() > 0) {
      return this.hoveredRating();
    }
    return this.currentRating();
  });

  // Generate array of star indices
  get stars(): number[] {
    return Array.from({ length: this.maxRating }, (_, i) => i + 1);
  }

  /**
   * Get the star type for rendering
   * @param starIndex - 1-based star index
   * @returns 'full', 'half', or 'empty'
   */
  getStarType(starIndex: number): 'full' | 'half' | 'empty' {
    const rating = this.displayRating();
    const diff = rating - starIndex;

    if (diff >= 0) {
      return 'full';
    } else if (diff > -1 && diff < 0) {
      return 'half';
    } else {
      return 'empty';
    }
  }

  /**
   * Handle mouse enter on a star (interactive mode only)
   */
  onStarHover(starIndex: number): void {
    if (this.interactive) {
      this.hoveredRating.set(starIndex);
    }
  }

  /**
   * Handle mouse leave from rating container
   */
  onMouseLeave(): void {
    if (this.interactive) {
      this.hoveredRating.set(0);
    }
  }

  /**
   * Handle star click (interactive mode only)
   */
  onStarClick(starIndex: number): void {
    if (this.interactive) {
      const newRating = this.clampRating(starIndex);
      this.currentRating.set(newRating);
      this.ratingChanged.emit(newRating);
    }
  }

  /**
   * Clamp rating between 0 and maxRating
   */
  private clampRating(value: number): number {
    return Math.max(0, Math.min(this.maxRating, value));
  }

  /**
   * Get ARIA label for the rating component
   */
  getAriaLabel(): string {
    const rating = this.currentRating();
    const ratingText = `${rating.toFixed(1)} out of ${this.maxRating} stars`;

    if (this.count !== undefined) {
      return `${ratingText}, based on ${this.count} ${this.count === 1 ? 'review' : 'reviews'}`;
    }

    return ratingText;
  }

  /**
   * Get ARIA label for individual star
   */
  getStarAriaLabel(starIndex: number): string {
    if (this.interactive) {
      return `Rate ${starIndex} out of ${this.maxRating} stars`;
    }
    return '';
  }
}
