import { Component, input, output, signal, effect, ElementRef, ViewChild, AfterViewInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface Brand {
  id: string | number;
  name: string;
  logoUrl: string;
}

@Component({
  selector: 'app-brand-bar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './brand-bar.component.html',
  styleUrls: ['./brand-bar.component.scss']
})
export class BrandBarComponent implements AfterViewInit, OnDestroy {
  @ViewChild('scrollContainer') scrollContainer!: ElementRef<HTMLDivElement>;

  brands = input.required<Brand[]>();
  autoScroll = input<boolean>(true);
  scrollSpeed = input<number>(30); // pixels per second

  brandClicked = output<Brand>();

  isHovered = signal(false);
  isPaused = signal(false);

  private animationFrameId: number | null = null;
  private scrollPosition = 0;
  private lastTimestamp = 0;

  ngAfterViewInit(): void {
    if (this.autoScroll()) {
      this.startAutoScroll();
    }
  }

  ngOnDestroy(): void {
    this.stopAutoScroll();
  }

  onBrandClick(brand: Brand): void {
    this.brandClicked.emit(brand);
  }

  onMouseEnter(): void {
    this.isHovered.set(true);
    if (this.autoScroll()) {
      this.isPaused.set(true);
    }
  }

  onMouseLeave(): void {
    this.isHovered.set(false);
    if (this.autoScroll()) {
      this.isPaused.set(false);
    }
  }

  scrollLeft(): void {
    const container = this.scrollContainer?.nativeElement;
    if (container) {
      container.scrollBy({
        left: -200,
        behavior: 'smooth'
      });
    }
  }

  scrollRight(): void {
    const container = this.scrollContainer?.nativeElement;
    if (container) {
      container.scrollBy({
        left: 200,
        behavior: 'smooth'
      });
    }
  }

  private startAutoScroll(): void {
    const animate = (timestamp: number) => {
      if (!this.lastTimestamp) {
        this.lastTimestamp = timestamp;
      }

      const container = this.scrollContainer?.nativeElement;
      if (!container) {
        this.animationFrameId = requestAnimationFrame(animate);
        return;
      }

      if (!this.isPaused()) {
        const deltaTime = (timestamp - this.lastTimestamp) / 1000;
        const scrollAmount = this.scrollSpeed() * deltaTime;

        this.scrollPosition += scrollAmount;

        // Reset scroll position when reaching the end
        const maxScroll = container.scrollWidth - container.clientWidth;
        if (this.scrollPosition >= maxScroll && maxScroll > 0) {
          this.scrollPosition = 0;
        }

        container.scrollLeft = this.scrollPosition;
      }

      this.lastTimestamp = timestamp;
      this.animationFrameId = requestAnimationFrame(animate);
    };

    this.animationFrameId = requestAnimationFrame(animate);
  }

  private stopAutoScroll(): void {
    if (this.animationFrameId !== null) {
      cancelAnimationFrame(this.animationFrameId);
      this.animationFrameId = null;
    }
  }

  trackByBrandId(index: number, brand: Brand): string | number {
    return brand.id;
  }
}
