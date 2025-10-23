import {
  Directive,
  ElementRef,
  Input,
  OnInit,
  OnDestroy,
  Renderer2,
  inject
} from '@angular/core';

/**
 * Lazy Load Image Directive
 *
 * A standalone directive that uses the Intersection Observer API to lazy load images
 * when they enter the viewport. Provides smooth fade-in animation and error handling.
 *
 * @example
 * ```html
 * <img
 *   [appLazyLoadImage]="'https://example.com/image.jpg'"
 *   [placeholder]="'assets/placeholder.png'"
 *   alt="Product image">
 * ```
 *
 * Features:
 * - Loads image only when element enters viewport
 * - Displays placeholder until actual image is loaded
 * - Smooth fade-in animation on load
 * - Handles loading errors with fallback image
 * - Properly cleans up observers on destroy
 * - Adds 'loaded' class when image successfully loads
 */
@Directive({
  selector: '[appLazyLoadImage]',
  standalone: true
})
export class LazyLoadImageDirective implements OnInit, OnDestroy {
  private readonly elementRef = inject(ElementRef<HTMLImageElement>);
  private readonly renderer = inject(Renderer2);

  /**
   * The URL of the image to lazy load
   */
  @Input({ required: true }) appLazyLoadImage!: string;

  /**
   * Optional placeholder image URL to display while loading
   * @default 'data:image/svg+xml,...' (1x1 transparent pixel)
   */
  @Input() placeholder?: string;

  private intersectionObserver?: IntersectionObserver;
  private readonly defaultPlaceholder = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg"%3E%3C/svg%3E';
  private readonly fallbackImage = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"%3E%3Crect fill="%23ddd" width="100" height="100"/%3E%3Ctext x="50" y="50" text-anchor="middle" dy=".3em" fill="%23999" font-family="sans-serif"%3EError%3C/text%3E%3C/svg%3E';

  ngOnInit(): void {
    this.setupLazyLoading();
  }

  ngOnDestroy(): void {
    this.cleanup();
  }

  /**
   * Sets up the Intersection Observer and initial placeholder
   */
  private setupLazyLoading(): void {
    const img = this.elementRef.nativeElement;

    // Set initial styles for fade-in animation
    this.renderer.setStyle(img, 'opacity', '0');
    this.renderer.setStyle(img, 'transition', 'opacity 0.3s ease-in-out');

    // Set placeholder image
    this.renderer.setAttribute(
      img,
      'src',
      this.placeholder || this.defaultPlaceholder
    );

    // Create and configure Intersection Observer
    this.intersectionObserver = new IntersectionObserver(
      (entries) => this.onIntersection(entries),
      {
        root: null, // viewport
        rootMargin: '50px', // start loading 50px before entering viewport
        threshold: 0.01 // trigger when even 1% is visible
      }
    );

    // Start observing
    this.intersectionObserver.observe(img);
  }

  /**
   * Handles intersection observer callback
   */
  private onIntersection(entries: IntersectionObserverEntry[]): void {
    entries.forEach((entry) => {
      if (entry.isIntersecting) {
        this.loadImage();
        // Stop observing once we start loading
        this.intersectionObserver?.unobserve(entry.target);
      }
    });
  }

  /**
   * Loads the actual image and handles success/error cases
   */
  private loadImage(): void {
    const img = this.elementRef.nativeElement;
    const imageToLoad = new Image();

    // Handle successful load
    imageToLoad.onload = () => {
      this.renderer.setAttribute(img, 'src', this.appLazyLoadImage);
      this.renderer.setStyle(img, 'opacity', '1');
      this.renderer.addClass(img, 'loaded');
    };

    // Handle load error
    imageToLoad.onerror = () => {
      console.warn(`Failed to load image: ${this.appLazyLoadImage}`);
      this.renderer.setAttribute(img, 'src', this.fallbackImage);
      this.renderer.setStyle(img, 'opacity', '1');
      this.renderer.addClass(img, 'error');
    };

    // Start loading
    imageToLoad.src = this.appLazyLoadImage;
  }

  /**
   * Cleans up the Intersection Observer
   */
  private cleanup(): void {
    if (this.intersectionObserver) {
      this.intersectionObserver.disconnect();
      this.intersectionObserver = undefined;
    }
  }
}
