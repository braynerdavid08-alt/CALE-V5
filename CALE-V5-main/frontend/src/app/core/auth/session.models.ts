export interface SessionUser {
  id: number;
  name: string;
  email: string;
  role: string;
  mustChangePassword?: boolean;
  schoolId?: number | null;
  isMembershipActive?: boolean;
  planLabel?: string | null;
  /** App gratis para todos: sin membresía ni escuela obligatoria. */
  freeAccess?: boolean;
  photoUrl?: string | null;
}

export interface AuthResponse {
  token: string;
  userId: number;
  name: string;
  email: string;
  role: string;
  mustChangePassword?: boolean;
  usesCookieAuth?: boolean;
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
  freeAccess?: boolean;
  photoUrl?: string | null;
}
