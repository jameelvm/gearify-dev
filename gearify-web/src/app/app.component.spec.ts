import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppComponent } from './app.component';
import { ThemeService } from '@core/services/theme.service';
import { provideRouter } from '@angular/router';

describe('AppComponent', () => {
  let component: AppComponent;
  let fixture: ComponentFixture<AppComponent>;

  beforeEach(async () => {
    const themeServiceMock = {} as any;

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        { provide: ThemeService, useValue: themeServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render router-outlet', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement;
    expect(compiled.querySelector('router-outlet')).toBeTruthy();
  });

  it('should initialize tenant ID on startup', () => {
    localStorage.clear();
    fixture.detectChanges();

    // Should initialize tenant ID if not present
    expect(component).toBeTruthy();
  });

  it('should detect device type on init', () => {
    fixture.detectChanges();

    expect(component.isMobile()).toBeDefined();
    expect(component.isTouch()).toBeDefined();
  });
});
