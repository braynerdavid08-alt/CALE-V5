import { TheoryBookingPresetDto, TheorySettingsDto } from './api/theory.api';

export interface BuiltinBookingPreset {
  key: string;
  name: string;
  summary: string;
  values: BookingPresetValues;
}

export type BookingPresetValues = Pick<
  TheorySettingsDto,
  | 'weekdaysEnabled'
  | 'saturdayEnabled'
  | 'maxWeekdayClassesPerDay'
  | 'maxSaturdayClassesPerDay'
  | 'maxDailyTheoryMinutes'
  | 'weekdayReservationOpenDaysBefore'
  | 'saturdayReservationOpenDaysBefore'
  | 'studentBookingWindowStart'
  | 'studentBookingWindowEnd'
>;

export const BUILTIN_BOOKING_PRESETS: BuiltinBookingPreset[] = [
  {
    key: 'standard',
    name: 'Estándar',
    summary: '1 entre semana · 4 sábados',
    values: {
      weekdaysEnabled: true,
      saturdayEnabled: true,
      maxWeekdayClassesPerDay: 1,
      maxSaturdayClassesPerDay: 4,
      maxDailyTheoryMinutes: 0,
      weekdayReservationOpenDaysBefore: 1,
      saturdayReservationOpenDaysBefore: 2,
      studentBookingWindowStart: null,
      studentBookingWindowEnd: null
    }
  },
  {
    key: 'strict',
    name: 'Estricto',
    summary: '1 clase/día · máx. 2 h',
    values: {
      weekdaysEnabled: true,
      saturdayEnabled: true,
      maxWeekdayClassesPerDay: 1,
      maxSaturdayClassesPerDay: 1,
      maxDailyTheoryMinutes: 120,
      weekdayReservationOpenDaysBefore: 1,
      saturdayReservationOpenDaysBefore: 2,
      studentBookingWindowStart: null,
      studentBookingWindowEnd: null
    }
  },
  {
    key: 'open',
    name: 'Abierto',
    summary: 'Sin límite de clases',
    values: {
      weekdaysEnabled: true,
      saturdayEnabled: true,
      maxWeekdayClassesPerDay: 0,
      maxSaturdayClassesPerDay: 0,
      maxDailyTheoryMinutes: 0,
      weekdayReservationOpenDaysBefore: 1,
      saturdayReservationOpenDaysBefore: 2,
      studentBookingWindowStart: null,
      studentBookingWindowEnd: null
    }
  },
  {
    key: 'flexible',
    name: 'Flexible',
    summary: '2 entre semana · 6 sábados · 4 h',
    values: {
      weekdaysEnabled: true,
      saturdayEnabled: true,
      maxWeekdayClassesPerDay: 2,
      maxSaturdayClassesPerDay: 6,
      maxDailyTheoryMinutes: 240,
      weekdayReservationOpenDaysBefore: 1,
      saturdayReservationOpenDaysBefore: 2,
      studentBookingWindowStart: null,
      studentBookingWindowEnd: null
    }
  }
];

export function bookingValuesFromSettings(cfg: TheorySettingsDto): BookingPresetValues {
  return {
    weekdaysEnabled: cfg.weekdaysEnabled,
    saturdayEnabled: cfg.saturdayEnabled,
    maxWeekdayClassesPerDay: cfg.maxWeekdayClassesPerDay,
    maxSaturdayClassesPerDay: cfg.maxSaturdayClassesPerDay,
    maxDailyTheoryMinutes: cfg.maxDailyTheoryMinutes,
    weekdayReservationOpenDaysBefore: cfg.weekdayReservationOpenDaysBefore,
    saturdayReservationOpenDaysBefore: cfg.saturdayReservationOpenDaysBefore,
    studentBookingWindowStart: cfg.studentBookingWindowStart ?? null,
    studentBookingWindowEnd: cfg.studentBookingWindowEnd ?? null
  };
}

export function applyBookingValues(
  cfg: TheorySettingsDto,
  values: BookingPresetValues
): TheorySettingsDto {
  return {
    ...cfg,
    ...values,
    studentBookingWindowStart: values.studentBookingWindowStart?.trim() || null,
    studentBookingWindowEnd: values.studentBookingWindowEnd?.trim() || null
  };
}

export function presetFromSettings(name: string, cfg: TheorySettingsDto): TheoryBookingPresetDto {
  const values = bookingValuesFromSettings(cfg);
  return {
    id: crypto.randomUUID(),
    name,
    ...values
  };
}

export interface BookingPresetListItem {
  id: string;
  name: string;
  summary?: string;
  builtin: boolean;
  values: BookingPresetValues;
}

export function listVisibleBookingPresets(
  hiddenKeys: string[],
  saved: TheoryBookingPresetDto[]
): BookingPresetListItem[] {
  const hidden = new Set(hiddenKeys.map((k) => k.toLowerCase()));
  const builtins = BUILTIN_BOOKING_PRESETS
    .filter((p) => !hidden.has(p.key.toLowerCase()))
    .map((p) => ({
      id: p.key,
      name: p.name,
      summary: p.summary,
      builtin: true,
      values: p.values
    }));

  const custom = saved.map((p) => ({
    id: p.id,
    name: p.name,
    summary: summarizePreset(p),
    builtin: false,
    values: {
      weekdaysEnabled: p.weekdaysEnabled,
      saturdayEnabled: p.saturdayEnabled,
      maxWeekdayClassesPerDay: p.maxWeekdayClassesPerDay,
      maxSaturdayClassesPerDay: p.maxSaturdayClassesPerDay,
      maxDailyTheoryMinutes: p.maxDailyTheoryMinutes,
      weekdayReservationOpenDaysBefore: p.weekdayReservationOpenDaysBefore,
      saturdayReservationOpenDaysBefore: p.saturdayReservationOpenDaysBefore,
      studentBookingWindowStart: p.studentBookingWindowStart ?? null,
      studentBookingWindowEnd: p.studentBookingWindowEnd ?? null
    }
  }));

  return [...builtins, ...custom];
}

function summarizePreset(p: TheoryBookingPresetDto): string {
  const weekday = p.maxWeekdayClassesPerDay === 0 ? '∞ semana' : `${p.maxWeekdayClassesPerDay} semana`;
  const saturday = p.maxSaturdayClassesPerDay === 0 ? '∞ sáb.' : `${p.maxSaturdayClassesPerDay} sáb.`;
  const minutes = p.maxDailyTheoryMinutes > 0 ? ` · ${p.maxDailyTheoryMinutes} min/día` : '';
  return `${weekday} · ${saturday}${minutes}`;
}
