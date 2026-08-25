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

export interface MeSchoolContext {
  schoolId: number;
  legalName: string;
  planLabel: string;
  city: string;
  department: string;
  subscriptionStatus: string;
  daysRemaining: number;
  isMembershipActive: boolean;
}

export interface MeResponse {
  id: number;
  name: string;
  email: string;
  role: string;
  isActive: boolean;
  createdAt: string;
  school?: MeSchoolContext | null;
}
