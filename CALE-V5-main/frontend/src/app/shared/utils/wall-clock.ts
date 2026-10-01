/**
 * Helpers for Colombia wall-clock values ('yyyy-MM-dd' dates and 'HH:mm' times).
 * Never pass these strings to `new Date(string)`: ISO dates are parsed as UTC and can shift the day.
 */

const COLOMBIA_TZ = 'America/Bogota';

const WEEKDAYS = ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado'];
const MONTHS = [
  'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
  'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'
];

/** .NET DayOfWeek numbering: 0 = domingo … 6 = sábado. */
export const DAY_NAMES = ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'];

/** Monday first, Sunday last. */
export const WEEK_ORDER = [1, 2, 3, 4, 5, 6, 0];

function capitalize(text: string): string {
  return text ? text.charAt(0).toUpperCase() + text.slice(1) : text;
}

export function parseYmd(value: string): Date {
  const [y, m, d] = value.slice(0, 10).split('-').map(Number);
  return new Date(y, (m || 1) - 1, d || 1, 12, 0, 0);
}

export function toYmd(date: Date): string {
  const mm = String(date.getMonth() + 1).padStart(2, '0');
  const dd = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${mm}-${dd}`;
}

export function addDays(ymd: string, days: number): string {
  const d = parseYmd(ymd);
  d.setDate(d.getDate() + days);
  return toYmd(d);
}

/** Today's date in Colombia, regardless of the device time zone. */
export function todayColombia(): string {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: COLOMBIA_TZ,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit'
  }).formatToParts(new Date());
  const get = (type: string) => parts.find((p) => p.type === type)?.value ?? '';
  return `${get('year')}-${get('month')}-${get('day')}`;
}

export function mondayOf(ymd: string): string {
  const day = parseYmd(ymd).getDay();
  return addDays(ymd, day === 0 ? -6 : 1 - day);
}

export function dayOfWeek(ymd: string): number {
  return parseYmd(ymd).getDay();
}

/** 'Lunes 5 de octubre' */
export function dayTitle(ymd: string): string {
  const d = parseYmd(ymd);
  return `${capitalize(WEEKDAYS[d.getDay()])} ${d.getDate()} de ${MONTHS[d.getMonth()]}`;
}

/** 'Semana del 5 al 11 de octubre' / 'Semana del 29 de septiembre al 5 de octubre' */
export function weekRangeLabel(from: string, to: string): string {
  const a = parseYmd(from);
  const b = parseYmd(to);
  const yearSuffix = a.getFullYear() !== b.getFullYear() ? ` de ${b.getFullYear()}` : '';
  const start = a.getMonth() === b.getMonth() && !yearSuffix
    ? `${a.getDate()}`
    : `${a.getDate()} de ${MONTHS[a.getMonth()]}${yearSuffix ? ` de ${a.getFullYear()}` : ''}`;
  return `Semana del ${start} al ${b.getDate()} de ${MONTHS[b.getMonth()]}${yearSuffix}`;
}

export function hhmm(time: string | null | undefined): string {
  return (time ?? '').slice(0, 5);
}

/** '09:00' → '9:00 a. m.', '14:30' → '2:30 p. m.' */
export function time12(time: string | null | undefined): string {
  const value = hhmm(time);
  if (!value) return '';
  const [hRaw, m = '00'] = value.split(':');
  const h = Number(hRaw);
  if (Number.isNaN(h)) return value;
  const suffix = h < 12 ? 'a. m.' : 'p. m.';
  const h12 = h % 12 === 0 ? 12 : h % 12;
  return `${h12}:${m} ${suffix}`;
}

/** Real instants (createdAt…) shown in Colombia time. */
export function formatInstant(value: string | null | undefined): string {
  if (!value) return '';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleString('es-CO', {
    timeZone: COLOMBIA_TZ,
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit'
  });
}

/**
 * Deadlines may come as an instant (with Z/offset) or as a Colombia wall-clock 'yyyy-MM-ddTHH:mm'.
 * Returns 'Lunes 5 de octubre, 9:00 a. m.'.
 */
export function formatDeadline(value: string | null | undefined): string {
  if (!value) return '';
  const hasZone = /([zZ]|[+-]\d{2}:?\d{2})$/.test(value);
  if (!hasZone && /^\d{4}-\d{2}-\d{2}/.test(value)) {
    const time = value.length > 11 ? value.slice(11, 16) : '';
    return time ? `${dayTitle(value)}, ${time12(time)}` : dayTitle(value);
  }
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: COLOMBIA_TZ,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23'
  }).formatToParts(d);
  const get = (type: string) => parts.find((p) => p.type === type)?.value ?? '';
  return `${dayTitle(`${get('year')}-${get('month')}-${get('day')}`)}, ${time12(`${get('hour')}:${get('minute')}`)}`;
}

export function hoursLabel(value: number): string {
  const rounded = Math.round(value * 10) / 10;
  const text = Number.isInteger(rounded) ? String(rounded) : rounded.toLocaleString('es-CO');
  return `${text} h`;
}
