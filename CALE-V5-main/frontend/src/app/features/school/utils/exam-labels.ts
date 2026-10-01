import { ExamBookingDto, ExamSlotDto } from '../api/exam-schedule.api';

export type Tone = 'success' | 'danger' | 'warning' | 'primary' | 'neutral';

export function slotStatusLabel(status: string): string {
  switch (status) {
    case 'Available': return 'Disponible';
    case 'Full': return 'Agotado';
    case 'Closed': return 'Cerrado';
    case 'Past': return 'Pasado';
    default: return status;
  }
}

export function slotStatusTone(status: string): Tone {
  switch (status) {
    case 'Available': return 'success';
    case 'Full': return 'danger';
    default: return 'neutral';
  }
}

export function bookingStatusLabel(b: ExamBookingDto): string {
  if (b.noShow || b.status === 'NoShow') return 'No vino';
  switch (b.status) {
    case 'Active': return b.checkedInAt ? 'Llegó' : 'Agendado';
    case 'Completed': return 'Presentó el examen';
    case 'Cancelled': return 'Cancelada';
    default: return b.status;
  }
}

export function bookingStatusTone(b: ExamBookingDto): Tone {
  if (b.noShow || b.status === 'NoShow') return 'danger';
  switch (b.status) {
    case 'Active': return b.checkedInAt ? 'success' : 'primary';
    case 'Completed': return 'success';
    default: return 'neutral';
  }
}

export function activeBookings(slot: ExamSlotDto): ExamBookingDto[] {
  return (slot.bookings ?? []).filter((b) => b.status !== 'Cancelled');
}

export function plural(count: number, one: string, many: string): string {
  return `${count} ${count === 1 ? one : many}`;
}
