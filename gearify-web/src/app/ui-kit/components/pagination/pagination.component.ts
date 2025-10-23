import { Component, input, output, signal, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface PageChangeEvent {
  page: number;
  itemsPerPage: number;
}

export interface ItemsPerPageChangeEvent {
  itemsPerPage: number;
}

@Component({
  selector: 'app-pagination',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './pagination.component.html',
  styleUrls: ['./pagination.component.scss']
})
export class PaginationComponent {
  // Inputs
  currentPage = input.required<number>();
  totalPages = input.required<number>();
  totalItems = input.required<number>();
  itemsPerPage = input<number>(10);
  itemsPerPageOptions = input<number[]>([10, 25, 50, 100]);
  maxVisiblePages = input<number>(5);
  showJumpToPage = input<boolean>(true);
  showItemsPerPageSelector = input<boolean>(true);
  showTotalItems = input<boolean>(true);

  // Outputs
  pageChanged = output<PageChangeEvent>();
  itemsPerPageChanged = output<ItemsPerPageChangeEvent>();

  // Local state
  jumpToPageValue = signal<number | null>(null);

  // Computed values
  isFirstPage = computed(() => this.currentPage() === 1);
  isLastPage = computed(() => this.currentPage() >= this.totalPages());

  startItem = computed(() => {
    const page = this.currentPage();
    const perPage = this.itemsPerPage();
    return (page - 1) * perPage + 1;
  });

  endItem = computed(() => {
    const page = this.currentPage();
    const perPage = this.itemsPerPage();
    const total = this.totalItems();
    const end = page * perPage;
    return end > total ? total : end;
  });

  visiblePageNumbers = computed(() => {
    const current = this.currentPage();
    const total = this.totalPages();
    const maxVisible = this.maxVisiblePages();

    if (total <= maxVisible) {
      return Array.from({ length: total }, (_, i) => i + 1);
    }

    const pages: (number | 'ellipsis')[] = [];
    const halfVisible = Math.floor(maxVisible / 2);

    let startPage = Math.max(1, current - halfVisible);
    let endPage = Math.min(total, current + halfVisible);

    if (current <= halfVisible) {
      endPage = maxVisible;
    } else if (current >= total - halfVisible) {
      startPage = total - maxVisible + 1;
    }

    if (startPage > 1) {
      pages.push(1);
      if (startPage > 2) {
        pages.push('ellipsis');
      }
    }

    for (let i = startPage; i <= endPage; i++) {
      pages.push(i);
    }

    if (endPage < total) {
      if (endPage < total - 1) {
        pages.push('ellipsis');
      }
      pages.push(total);
    }

    return pages;
  });

  constructor() {
    // Sync jump to page value with current page
    effect(() => {
      this.jumpToPageValue.set(this.currentPage());
    });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) {
      return;
    }

    this.pageChanged.emit({
      page,
      itemsPerPage: this.itemsPerPage()
    });
  }

  goToPreviousPage(): void {
    if (!this.isFirstPage()) {
      this.goToPage(this.currentPage() - 1);
    }
  }

  goToNextPage(): void {
    if (!this.isLastPage()) {
      this.goToPage(this.currentPage() + 1);
    }
  }

  onJumpToPage(): void {
    const page = this.jumpToPageValue();
    if (page !== null && page >= 1 && page <= this.totalPages()) {
      this.goToPage(page);
    } else {
      this.jumpToPageValue.set(this.currentPage());
    }
  }

  onItemsPerPageChange(value: string): void {
    const itemsPerPage = parseInt(value, 10);
    if (!isNaN(itemsPerPage) && itemsPerPage > 0) {
      this.itemsPerPageChanged.emit({ itemsPerPage });

      // Adjust current page if needed to stay within bounds
      const newTotalPages = Math.ceil(this.totalItems() / itemsPerPage);
      if (this.currentPage() > newTotalPages) {
        this.pageChanged.emit({
          page: newTotalPages || 1,
          itemsPerPage
        });
      }
    }
  }

  trackByIndex(index: number): number {
    return index;
  }

  isEllipsis(item: number | 'ellipsis'): boolean {
    return item === 'ellipsis';
  }

  isNumber(item: number | 'ellipsis'): item is number {
    return typeof item === 'number';
  }
}
