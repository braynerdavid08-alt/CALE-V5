export type SchoolBadgeTone = 'neutral' | 'ok' | 'warn' | 'info' | 'danger';

export interface SchoolBadge {
  label: string;
  tone: SchoolBadgeTone;
}

export interface StudentBadgeInput {
  status?: string;
  balanceDue?: number;
  theoryExamAuthorized?: boolean;
  practicalAuthorized?: boolean;
  isEnrolled?: boolean;
  runtRegistered?: boolean;
  theoryHoursComplete?: boolean;
  workshopHoursComplete?: boolean;
  theoryExamPassed?: boolean;
}

export function buildStudentBadges(row: StudentBadgeInput): SchoolBadge[] {
  const badges: SchoolBadge[] = [];

  if ((row.balanceDue ?? 0) > 0) {
    badges.push({ label: 'Saldo', tone: 'warn' });
  }

  if (row.status === 'Suspended') {
    badges.push({ label: 'Suspendido', tone: 'danger' });
  } else if (row.status === 'Pending' || row.status === 'Accepted') {
    badges.push({ label: 'Pendiente', tone: 'neutral' });
  } else if (row.status === 'Active') {
    badges.push({ label: 'Activo', tone: 'ok' });
  }

  if (row.theoryExamAuthorized) {
    badges.push({ label: 'Examen ✓', tone: 'info' });
  } else if (
    row.theoryHoursComplete
    && row.workshopHoursComplete
    && !row.theoryExamPassed
  ) {
    badges.push({ label: 'Listo examen', tone: 'ok' });
  }

  if (row.practicalAuthorized) {
    badges.push({ label: 'Manejo ✓', tone: 'ok' });
  } else if (row.theoryExamPassed) {
    badges.push({ label: 'Listo manejo', tone: 'info' });
  }

  if (row.isEnrolled) {
    badges.push({ label: 'Enrolado', tone: 'ok' });
  }

  if (row.runtRegistered) {
    badges.push({ label: 'RUNT', tone: 'neutral' });
  }

  return badges;
}
