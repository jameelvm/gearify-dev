import { Component, Input, forwardRef, signal, effect, ElementRef, ViewChild, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, ReactiveFormsModule, FormsModule } from '@angular/forms';

export interface SelectOption {
  value: any;
  label: string;
}

/**
 * Custom Select component compatible with Reactive Forms
 * Features: Custom dropdown, searchable options, keyboard navigation
 */
@Component({
  selector: 'app-select',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './select.component.html',
  styleUrl: './select.component.scss',
  providers: [{
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(() => SelectComponent),
    multi: true
  }]
})
export class SelectComponent implements ControlValueAccessor {
  @Input() label?: string;
  @Input() options: SelectOption[] = [];
  @Input() error?: string;
  @Input() disabled = false;
  @Input() placeholder = 'Select an option';
  @Input() searchable = false;

  @ViewChild('searchInput') searchInput?: ElementRef<HTMLInputElement>;

  value = signal<any>(null);
  isOpen = signal<boolean>(false);
  searchQuery = signal<string>('');
  filteredOptions = signal<SelectOption[]>([]);
  highlightedIndex = signal<number>(-1);

  onChange: any = () => {};
  onTouched: any = () => {};

  constructor() {
    // Update filtered options when search query or options change
    effect(() => {
      const query = this.searchQuery().toLowerCase();
      const opts = this.options;

      if (!query) {
        this.filteredOptions.set(opts);
      } else {
        this.filteredOptions.set(
          opts.filter(opt => opt.label.toLowerCase().includes(query))
        );
      }
    });
  }

  writeValue(value: any): void {
    this.value.set(value);
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
  }

  toggleDropdown(): void {
    if (this.disabled) return;

    this.isOpen.update(open => !open);

    if (this.isOpen() && this.searchable) {
      // Focus search input when dropdown opens
      setTimeout(() => this.searchInput?.nativeElement.focus(), 0);
    }

    if (!this.isOpen()) {
      this.searchQuery.set('');
      this.highlightedIndex.set(-1);
    }
  }

  selectOption(option: SelectOption): void {
    if (this.disabled) return;

    this.value.set(option.value);
    this.onChange(option.value);
    this.onTouched();
    this.isOpen.set(false);
    this.searchQuery.set('');
    this.highlightedIndex.set(-1);
  }

  getSelectedLabel(): string {
    const selected = this.options.find(opt => opt.value === this.value());
    return selected ? selected.label : this.placeholder;
  }

  onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchQuery.set(value);
    this.highlightedIndex.set(0);
  }

  onKeyDown(event: KeyboardEvent): void {
    if (this.disabled) return;

    const filtered = this.filteredOptions();
    const currentIndex = this.highlightedIndex();

    switch (event.key) {
      case 'Enter':
        event.preventDefault();
        if (this.isOpen()) {
          if (currentIndex >= 0 && currentIndex < filtered.length) {
            this.selectOption(filtered[currentIndex]);
          }
        } else {
          this.toggleDropdown();
        }
        break;

      case 'ArrowDown':
        event.preventDefault();
        if (!this.isOpen()) {
          this.toggleDropdown();
        } else {
          this.highlightedIndex.update(i =>
            i < filtered.length - 1 ? i + 1 : i
          );
        }
        break;

      case 'ArrowUp':
        event.preventDefault();
        if (this.isOpen()) {
          this.highlightedIndex.update(i => i > 0 ? i - 1 : 0);
        }
        break;

      case 'Escape':
        event.preventDefault();
        this.isOpen.set(false);
        this.searchQuery.set('');
        this.highlightedIndex.set(-1);
        break;

      case 'Tab':
        if (this.isOpen()) {
          this.isOpen.set(false);
          this.searchQuery.set('');
          this.highlightedIndex.set(-1);
        }
        break;
    }
  }

  @HostListener('document:click', ['$event'])
  onClickOutside(event: Event): void {
    const target = event.target as HTMLElement;
    const clickedInside = target.closest('.select-wrapper');

    if (!clickedInside && this.isOpen()) {
      this.isOpen.set(false);
      this.searchQuery.set('');
      this.highlightedIndex.set(-1);
    }
  }

  trackByValue(index: number, option: SelectOption): any {
    return option.value;
  }
}
