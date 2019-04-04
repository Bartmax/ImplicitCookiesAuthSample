import { Component, OnInit } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../services/auth.service';

@Component({
  selector: 'app-register',
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.css']
})
export class RegisterComponent {
  registerFeedback: string = '';
  constructor(private authService: AuthService) { }
  register(email: string, password: string, repassword: string) {
    this.registerFeedback = '';
    if (email === '' || password === '' || repassword === '') {
      this.registerFeedback = 'All fields are required';
      return;
    }
    if (password !== repassword) {
      this.registerFeedback = 'Password don\'t match';
      return;
    }

    this.authService.register(email, password).subscribe(_ => {
      this.registerFeedback = `${email} registered with success, you can now log in`
    }, (errorResponse: HttpErrorResponse) => {
      this.registerFeedback = errorResponse.error;
    });
  }

}
