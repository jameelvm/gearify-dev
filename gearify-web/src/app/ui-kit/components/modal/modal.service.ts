import { Injectable, Type, ComponentRef, ViewContainerRef, ApplicationRef, createComponent, EnvironmentInjector, inject } from '@angular/core';
import { Subject, Observable } from 'rxjs';
import { ModalComponent, ModalSize } from './modal.component';

export interface ModalConfig<T = any> {
  size?: ModalSize;
  closeOnBackdrop?: boolean;
  title?: string;
  showCloseButton?: boolean;
  data?: T;
}

export interface ModalRef<T = any, R = any> {
  close: (result?: R) => void;
  data?: T;
  afterClosed: Observable<R | undefined>;
}

/**
 * Service to programmatically open and close modals
 *
 * @example
 * constructor(private modalService: ModalService) {}
 *
 * openModal() {
 *   const modalRef = this.modalService.open(MyComponent, {
 *     size: 'medium',
 *     title: 'My Modal',
 *     data: { userId: 123 }
 *   });
 *
 *   modalRef.afterClosed.subscribe(result => {
 *     console.log('Modal closed with result:', result);
 *   });
 * }
 */
@Injectable({
  providedIn: 'root'
})
export class ModalService {
  private appRef = inject(ApplicationRef);
  private injector = inject(EnvironmentInjector);
  private modalComponentRef: ComponentRef<ModalComponent> | null = null;
  private contentComponentRef: ComponentRef<any> | null = null;
  private closeSubject: Subject<any> | null = null;

  /**
   * Opens a modal with the specified component and configuration
   * @param component The component to render inside the modal
   * @param config Configuration options for the modal
   * @returns ModalRef with methods to interact with the modal
   */
  open<T = any, R = any>(
    component: Type<any>,
    config: ModalConfig<T> = {}
  ): ModalRef<T, R> {
    // Close any existing modal
    this.close();

    // Create subject for close event
    this.closeSubject = new Subject<R | undefined>();

    // Get root view container
    const rootViewContainer = this.appRef.components[0].instance as any;
    const viewContainerRef: ViewContainerRef = rootViewContainer.viewContainerRef || this.appRef.components[0].injector.get(ViewContainerRef);

    // Create modal component
    this.modalComponentRef = createComponent(ModalComponent, {
      environmentInjector: this.injector,
      elementInjector: viewContainerRef.injector
    });

    // Configure modal
    const modalInstance = this.modalComponentRef.instance;
    modalInstance.size = config.size || 'medium';
    modalInstance.closeOnBackdrop = config.closeOnBackdrop !== undefined ? config.closeOnBackdrop : true;
    modalInstance.title = config.title;
    modalInstance.showCloseButton = config.showCloseButton !== undefined ? config.showCloseButton : true;

    // Create content component
    this.contentComponentRef = createComponent(component, {
      environmentInjector: this.injector,
      elementInjector: this.modalComponentRef.injector
    });

    // Pass data to content component if provided
    if (config.data && this.contentComponentRef.instance) {
      Object.assign(this.contentComponentRef.instance, { modalData: config.data });
    }

    // Get the body slot element and append content component
    const modalElement = this.modalComponentRef.location.nativeElement as HTMLElement;

    // Attach modal to DOM
    const hostElement = document.body;
    hostElement.appendChild(modalElement);

    // Wait for modal template to render, then append content
    setTimeout(() => {
      const bodySlot = modalElement.querySelector('.modal-body');
      if (bodySlot && this.contentComponentRef) {
        bodySlot.appendChild(this.contentComponentRef.location.nativeElement);
        this.contentComponentRef.changeDetectorRef.detectChanges();
      }
    }, 0);

    // Detect changes
    this.modalComponentRef.changeDetectorRef.detectChanges();

    // Subscribe to modal close event
    const closedSubscription = modalInstance.closed.subscribe(() => {
      this.close();
    });

    // Create modal reference
    const modalRef: ModalRef<T, R> = {
      close: (result?: R) => {
        if (this.closeSubject) {
          this.closeSubject.next(result);
          this.closeSubject.complete();
        }
        this.close();
      },
      data: config.data,
      afterClosed: this.closeSubject.asObservable()
    };

    // Provide modalRef to content component if it has a property for it
    if (this.contentComponentRef.instance && 'modalRef' in this.contentComponentRef.instance) {
      this.contentComponentRef.instance.modalRef = modalRef;
    }

    return modalRef;
  }

  /**
   * Closes the currently open modal
   * @param result Optional result to pass to afterClosed subscribers
   */
  close(result?: any): void {
    // Emit result and complete subject
    if (this.closeSubject) {
      this.closeSubject.next(result);
      this.closeSubject.complete();
      this.closeSubject = null;
    }

    // Destroy content component
    if (this.contentComponentRef) {
      this.contentComponentRef.destroy();
      this.contentComponentRef = null;
    }

    // Destroy modal component
    if (this.modalComponentRef) {
      const modalElement = this.modalComponentRef.location.nativeElement;

      // Wait for close animation before destroying
      setTimeout(() => {
        if (this.modalComponentRef) {
          this.modalComponentRef.destroy();
          this.modalComponentRef = null;
        }

        // Remove element from DOM if still present
        if (modalElement && modalElement.parentNode) {
          modalElement.parentNode.removeChild(modalElement);
        }
      }, 250);
    }
  }

  /**
   * Checks if a modal is currently open
   * @returns true if a modal is open
   */
  isOpen(): boolean {
    return this.modalComponentRef !== null;
  }
}
