export interface SessionUser {
  id: number;
  name: string;
  email: string;
  role: string;
  mustChangePassword?: boolean;
  schoolId?: number | null;
  isMembershipActive?: boolean;
  planLabel?: string | null;
}

export interface AuthResponse {
  token: string;
  userId: number;
  name: string;
  email: string;
  role: string;
  mustChangePassword?: boolean;
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
  mustChangePassword?: boolean;
  school?: MeSchoolContext | null;
}
