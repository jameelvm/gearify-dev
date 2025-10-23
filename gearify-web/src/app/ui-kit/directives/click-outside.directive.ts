import {
  Directive,
  ElementRef,
  EventEmitter,
  Output,
  OnInit,
  OnDestroy,
  inject,
  NgZone
} from '@angular/core';
import { fromEvent, Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';

/**
 * Click Outside Directive
 *
 * A standalone directive that detects clicks outside the host element and emits an event.
 * Useful for closing dropdowns, modals, popups, and other UI components that should
 * close when clicking outside.
 *
 * @example
 * ```html
 * <div class="dropdown" [appClickOutside]="closeDropdown()">
 *   <!-- Dropdown content -->
 * </div>
 * ```
 *
 * @example
 * ```html
 * <div
 *   class="modal"
 *   [appClickOutside]
 *   (clickOutside)="handleClickOutside()">
 *   <!-- Modal content -->
 * </div>
 * ```
 *
 * Features:
 * - Detects clicks outside the host element
 * - Emits event when outside click is detected
 * - Ignores clicks on the element itself and its children
 * - Properly manages event listeners and cleanup
 * - Runs outside Angular zone for better performance
 * - Handles edge cases like touch events
 */
@Directive({
  selector: '[appClickOutside]',
  standalone: true
})
export class ClickOutsideDirective implements OnInit, OnDestroy {
  private readonly elementRef = inject(ElementRef);
  private readonly ngZone = inject(NgZone);

  /**
   * Event emitted when a click occurs outside the host element
   */
  @Output() clickOutside = new EventEmitter<MouseEvent>();

  private clickSubscription?: Subscription;
  private touchSubscription?: Subscription;

  ngOnInit(): void {
    this.setupClickListener();
    this.setupTouchListener();
  }

  ngOnDestroy(): void {
    this.cleanup();
  }

  /**
   * Sets up the document click listener outside Angular zone for better performance
   */
  private setupClickListener(): void {
    // Run outside Angular zone to avoid triggering change detection on every click
    this.ngZone.runOutsideAngular(() => {
      // Delay subscription to avoid immediate triggering on the same click
      // that might have opened the element
      setTimeout(() => {
        this.clickSubscription = fromEvent<MouseEvent>(document, 'click', {
          capture: true
        })
          .pipe(
            filter((event) => this.shouldEmitEvent(event))
          )
          .subscribe((event) => {
            // Run inside Angular zone when emitting to ensure change detection
            this.ngZone.run(() => {
              this.clickOutside.emit(event);
            });
          });
      }, 0);
    });
  }

  /**
   * Sets up touch event listener for mobile devices
   */
  private setupTouchListener(): void {
    this.ngZone.runOutsideAngular(() => {
      setTimeout(() => {
        this.touchSubscription = fromEvent<TouchEvent>(document, 'touchstart', {
          capture: true
        })
          .pipe(
            filter((event) => {
              // Convert TouchEvent to a format compatible with shouldEmitEvent
              const touch = event.touches[0];
              if (!touch) return false;

              const mouseEvent = new MouseEvent('click', {
                clientX: touch.clientX,
                clientY: touch.clientY,
                bubbles: true
              });

              Object.defineProperty(mouseEvent, 'target', {
                value: event.target,
                enumerable: true
              });

              return this.shouldEmitEvent(mouseEvent);
            })
          )
          .subscribe((event) => {
            this.ngZone.run(() => {
              // Create a synthetic mouse event for consistency
              const touch = event.touches[0];
              const syntheticEvent = new MouseEvent('click', {
                clientX: touch.clientX,
                clientY: touch.clientY,
                bubbles: true
              });

              Object.defineProperty(syntheticEvent, 'target', {
                value: event.target,
                enumerable: true
              });

              this.clickOutside.emit(syntheticEvent);
            });
          });
      }, 0);
    });
  }

  /**
   * Determines if the event should trigger the clickOutside emission
   *
   * @param event The mouse event to check
   * @returns true if the click was outside the element, false otherwise
   */
  private shouldEmitEvent(event: MouseEvent): boolean {
    const clickTarget = event.target as Node;
    const hostElement = this.elementRef.nativeElement;

    // Don't emit if:
    // 1. Click target is the host element itself
    // 2. Click target is contained within the host element
    const isClickInside = hostElement === clickTarget ||
                          hostElement.contains(clickTarget);

    return !isClickInside;
  }

  /**
   * Cleans up event listeners and subscriptions
   */
  private cleanup(): void {
    if (this.clickSubscription) {
      this.clickSubscription.unsubscribe();
      this.clickSubscription = undefined;
    }

    if (this.touchSubscription) {
      this.touchSubscription.unsubscribe();
      this.touchSubscription = undefined;
    }
  }
}
