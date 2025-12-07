import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RegisterComponent } from './register.component';
import { AuthService } from '@core/services/auth.service';
import { Router, provideRouter } from '@angular/router';
import { Component } from '@angular/core';

// Dummy components for routing
@Component({ template: '' })
class DummyComponent {}

describe('RegisterComponent', () => {
  let component: RegisterComponent;
  let fixture: ComponentFixture<RegisterComponent>;

  beforeEach(async () => {
    const authServiceMock = {
      register: jest.fn()
    } as any;

    await TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [
        provideRouter([
          { path: 'auth/login', component: DummyComponent }
        ]),
        { provide: AuthService, useValue: authServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render registration form', () => {
    const compiled = fixture.nativeElement;
    expect(compiled.querySelector('form')).toBeTruthy();
    expect(compiled.querySelector('input[formControlName="email"]')).toBeTruthy();
    expect(compiled.querySelector('input[formControlName="password"]')).toBeTruthy();
    expect(compiled.querySelector('input[formControlName="firstName"]')).toBeTruthy();
    expect(compiled.querySelector('input[formControlName="lastName"]')).toBeTruthy();
  });

  it('should initialize with empty form', () => {
    expect(component.registerForm.get('email')?.value).toBe('');
    expect(component.registerForm.get('password')?.value).toBe('');
    expect(component.registerForm.get('firstName')?.value).toBe('');
    expect(component.registerForm.get('lastName')?.value).toBe('');
  });

  it('should validate required fields', () => {
    const form = component.registerForm;
    expect(form.valid).toBe(false);

    form.patchValue({
      email: 'test@test.com',
      password: 'Password123!',
      confirmPassword: 'Password123!',
      firstName: 'Test',
      lastName: 'User'
    });

    expect(form.valid).toBe(true);
  });
});
