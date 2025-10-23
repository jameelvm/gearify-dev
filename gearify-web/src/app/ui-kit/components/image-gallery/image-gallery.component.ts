import { Component, input, output, signal, effect, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface GalleryImage {
  url: string;
  alt: string;
  thumbnail?: string;
}

@Component({
  selector: 'app-image-gallery',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './image-gallery.component.html',
  styleUrls: ['./image-gallery.component.scss']
})
export class ImageGalleryComponent {
  images = input.required<GalleryImage[]>();
  imageClicked = output<GalleryImage>();

  currentIndex = signal(0);
  isFullscreen = signal(false);
  isZoomed = signal(false);
  touchStartX = signal(0);
  touchEndX = signal(0);

  currentImage = signal<GalleryImage | null>(null);

  constructor() {
    effect(() => {
      const imgs = this.images();
      if (imgs && imgs.length > 0) {
        this.currentImage.set(imgs[this.currentIndex()]);
      }
    });
  }

  selectImage(index: number): void {
    if (index >= 0 && index < this.images().length) {
      this.currentIndex.set(index);
      this.currentImage.set(this.images()[index]);
    }
  }

  previous(): void {
    const newIndex = this.currentIndex() > 0
      ? this.currentIndex() - 1
      : this.images().length - 1;
    this.selectImage(newIndex);
  }

  next(): void {
    const newIndex = this.currentIndex() < this.images().length - 1
      ? this.currentIndex() + 1
      : 0;
    this.selectImage(newIndex);
  }

  toggleFullscreen(): void {
    this.isFullscreen.set(!this.isFullscreen());
  }

  closeFullscreen(): void {
    this.isFullscreen.set(false);
    this.isZoomed.set(false);
  }

  onImageClick(image: GalleryImage): void {
    this.imageClicked.emit(image);
  }

  onMainImageClick(): void {
    this.toggleFullscreen();
  }

  // Touch event handlers
  onTouchStart(event: TouchEvent): void {
    this.touchStartX.set(event.touches[0].clientX);
  }

  onTouchMove(event: TouchEvent): void {
    this.touchEndX.set(event.touches[0].clientX);
  }

  onTouchEnd(): void {
    const swipeThreshold = 50;
    const diff = this.touchStartX() - this.touchEndX();

    if (Math.abs(diff) > swipeThreshold) {
      if (diff > 0) {
        this.next();
      } else {
        this.previous();
      }
    }

    this.touchStartX.set(0);
    this.touchEndX.set(0);
  }

  // Keyboard navigation
  @HostListener('window:keydown', ['$event'])
  handleKeyboardEvent(event: KeyboardEvent): void {
    if (!this.isFullscreen()) return;

    switch (event.key) {
      case 'ArrowLeft':
        event.preventDefault();
        this.previous();
        break;
      case 'ArrowRight':
        event.preventDefault();
        this.next();
        break;
      case 'Escape':
        event.preventDefault();
        this.closeFullscreen();
        break;
    }
  }

  getThumbnail(image: GalleryImage): string {
    return image.thumbnail || image.url;
  }

  trackByUrl(index: number, image: GalleryImage): string {
    return image.url;
  }
}
