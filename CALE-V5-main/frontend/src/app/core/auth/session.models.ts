export interface SessionUser {
  id: number;
  name: string;
  email: string;
  role: string;
}

export interface AuthResponse {
  token: string;
  userId: number;
  name: string;
  email: string;
  role: string;
}

export interface MeResponse {
  id: number;
  name: string;
  email: string;
  role: string;
  isActive: boolean;
}
