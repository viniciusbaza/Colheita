import { post } from './http'

export interface LoginRequest {
  username: string
  password: string
}

export interface LoginResponse {
  access_token: string
}

export interface RegisterRequest {
  username: string
  password: string
}

export interface RegisterResponse {
  userId: string
  username: string
  farmId: string
}

export async function loginRequest(data: LoginRequest) {
  return post<LoginResponse>('/auth/login', data)
}

export async function registerRequest(data: RegisterRequest) {
  return post<RegisterResponse>('/auth/register', data)
}