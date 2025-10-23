import { Component, input, output, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';

export interface BreadcrumbItem {
  label: string;
  url?: string;
  icon?: string;
}

@Component({
  selector: 'app-breadcrumb',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './breadcrumb.component.html',
  styleUrls: ['./breadcrumb.component.scss']
})
export class BreadcrumbComponent {
  // Inputs
  items = input.required<BreadcrumbItem[]>();
  separator = input<string>('/');
  showHomeIcon = input<boolean>(true);

  // Outputs
  itemClicked = output<BreadcrumbItem>();

  // Computed values
  processedItems = computed(() => {
    const itemsList = this.items();
    return itemsList.map((item, index) => ({
      ...item,
      isFirst: index === 0,
      isLast: index === itemsList.length - 1
    }));
  });

  constructor(private router: Router) {}

  onItemClick(item: BreadcrumbItem, event: Event): void {
    event.preventDefault();

    this.itemClicked.emit(item);

    if (item.url) {
      this.router.navigate([item.url]);
    }
  }

  trackByIndex(index: number): number {
    return index;
  }
}
