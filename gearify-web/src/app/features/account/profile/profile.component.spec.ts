import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ProfileComponent } from './profile.component';
import { AuthService } from '@features/auth/auth.service';
import { Router, provideRouter } from '@angular/router';
import { signal } from '@angular/core';

describe('ProfileComponent', () => {
  let component: ProfileComponent;
  let fixture: ComponentFixture<ProfileComponent>;
  let authService: jest.Mocked<AuthService>;

  const mockUser = {
    id: '123',
    email: 'test@example.com',
    firstName: 'Test',
    lastName: 'User',
    role: 'Customer',
    tenantId: 'default',
    isActive: true,
    emailVerified: true,
    phone: '+1234567890',
    lastLoginAt: new Date()
  };

  beforeEach(async () => {
    const authServiceMock = {
      user: jest.fn().mockReturnValue(signal({ user: mockUser, tokens: null, isAuthenticated: true, isLoading: false }))
    } as any;

    await TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authServiceMock }
      ]
    }).compileComponents();

    authService = TestBed.inject(AuthService) as jest.Mocked<AuthService>;
    fixture = TestBed.createComponent(ProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize profile form with user data', () => {
    expect(component.profileForm.get('firstName')?.value).toBe('Test');
    expect(component.profileForm.get('lastName')?.value).toBe('User');
    expect(component.profileForm.get('email')?.value).toBe('test@example.com');
    expect(component.profileForm.get('phone')?.value).toBe('+1234567890');
  });

  it('should have email field disabled', () => {
    expect(component.profileForm.get('email')?.disabled).toBe(true);
  });

  it('should toggle edit mode', () => {
    expect(component.isEditMode).toBe(false);
    component.toggleEditMode();
    expect(component.isEditMode).toBe(true);
    component.toggleEditMode();
    expect(component.isEditMode).toBe(false);
  });

  it('should toggle password change mode', () => {
    expect(component.isChangingPassword).toBe(false);
    component.togglePasswordChange();
    expect(component.isChangingPassword).toBe(true);
    component.togglePasswordChange();
    expect(component.isChangingPassword).toBe(false);
  });

  it('should validate password form', () => {
    const passwordForm = component.passwordForm;

    // Initially invalid
    expect(passwordForm.valid).toBe(false);

    // Set valid passwords
    passwordForm.patchValue({
      currentPassword: 'oldpass123',
      newPassword: 'newpass123',
      confirmPassword: 'newpass123'
    });

    expect(passwordForm.valid).toBe(true);
  });

  it('should detect password mismatch', () => {
    component.passwordForm.patchValue({
      currentPassword: 'oldpass123',
      newPassword: 'newpass123',
      confirmPassword: 'different123'
    });

    expect(component.passwordForm.errors?.['passwordMismatch']).toBe(true);
  });

  it('should return user initials correctly', () => {
    const initials = component.getUserInitials();
    expect(initials).toBe('TU');
  });
});
