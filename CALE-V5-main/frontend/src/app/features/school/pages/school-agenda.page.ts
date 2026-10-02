import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import {
  addDays,
  dayTitle,
  hhmm,
  mondayOf,
  time12,
  todayColombia,
  weekRangeLabel
} from '../../../shared/utils/wall-clock';
import { AgendaItemDto, AgendaItemKind, ExamScheduleApi } from '../api/exam-schedule.api';
import { Tone, plural } from '../utils/exam-labels';

type Filter = 'all' | AgendaItemKind;

interface AgendaDay {
  date: string;
  title: string;
  isToday: boolean;
  items: AgendaItemDto[];
}

const KIND_LABELS: Record<AgendaItemKind, string> = {
  theory: 'Clase teórica',
  practical: 'Clase de manejo',
  exam: 'Examen'
};

const STATUS_LABELS: Record<string, string> = {
  scheduled: 'Programada',
  active: 'Activa',
  available: 'Con cupos',
  full: 'Llena',
  closed: 'Cerrada',
  past: 'Ya pasó',
  completed: 'Terminada',
  cancelled: 'Cancelada',
  inprogress: 'En curso',
  reserved: 'Reservado',
  booked: 'Agendado',
  attended: 'Asistió',
  present: 'Asistió',
  absent: 'No vino',
  noshow: 'No vino',
  checkedin: 'Llegó',
  pending: 'Pendiente'
};

@Component({
  selector: 'app-school-agenda-page',
  standalone: true,
  imports: [RouterLink, UiErrorComponent, UiLoadingComponent],
  templateUrl: './school-agenda.page.html',
  styleUrls: ['../../../shared/styles/easy-schedule.css', './school-agenda.page.css']
})
export class SchoolAgendaPage implements OnInit {
  private readonly api = inject(ExamScheduleApi);

  readonly today = todayColombia();
  readonly weekStart = signal(mondayOf(this.today));
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<AgendaItemDto[]>([]);
  readonly filter = signal<Filter>('all');
  readonly expanded = signal<Set<string>>(new Set());

  readonly filters: { value: Filter; label: string }[] = [
    { value: 'all', label: 'Todo' },
    { value: 'theory', label: 'Clases teóricas' },
    { value: 'practical', label: 'Clases de manejo' },
    { value: 'exam', label: 'Exámenes' }
  ];

  readonly weekEnd = computed(() => addDays(this.weekStart(), 6));
  readonly weekLabel = computed(() => weekRangeLabel(this.weekStart(), this.weekEnd()));
  readonly isThisWeek = computed(() => this.weekStart() === mondayOf(this.today));

  readonly days = computed<AgendaDay[]>(() => {
    const f = this.filter();
    const visible = this.items().filter((i) => f === 'all' || i.kind === f);
    return Array.from({ length: 7 }, (_, i) => {
      const date = addDays(this.weekStart(), i);
      return {
        date,
        title: dayTitle(date),
        isToday: date === this.today,
        items: visible
          .filter((it) => it.date.slice(0, 10) === date)
          .sort((a, b) => hhmm(a.startTime).localeCompare(hhmm(b.startTime)))
      };
    });
  });

  readonly visibleCount = computed(() => this.days().reduce((n, d) => n + d.items.length, 0));

  readonly time12 = time12;
  readonly plural = plural;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getAgenda(this.weekStart(), this.weekEnd()).subscribe({
      next: (res) => {
        this.items.set(res.items ?? []);
        this.loading.set(false);
      },
      error: (err) => {
        this.items.set([]);
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  goWeek(offsetWeeks: number): void {
    this.weekStart.set(
      offsetWeeks === 0 ? mondayOf(this.today) : addDays(this.weekStart(), offsetWeeks * 7)
    );
    this.expanded.set(new Set());
    this.load();
  }

  key(item: AgendaItemDto): string {
    return `${item.kind}-${item.id}-${item.date}-${item.startTime}`;
  }

  isOpen(item: AgendaItemDto): boolean {
    return this.expanded().has(this.key(item));
  }

  toggle(item: AgendaItemDto): void {
    const k = this.key(item);
    this.expanded.update((set) => {
      const next = new Set(set);
      if (next.has(k)) next.delete(k);
      else next.add(k);
      return next;
    });
  }

  kindLabel(kind: AgendaItemKind): string {
    return KIND_LABELS[kind] ?? kind;
  }

  timeRange(item: AgendaItemDto): string {
    const start = time12(item.startTime);
    return item.endTime ? `${start} – ${time12(item.endTime)}` : start;
  }

  statusLabel(status: string | null | undefined): string {
    if (!status) return '';
    return STATUS_LABELS[status.replace(/[\s_-]/g, '').toLowerCase()] ?? status;
  }

  statusTone(status: string | null | undefined): Tone {
    const s = (status ?? '').replace(/[\s_-]/g, '').toLowerCase();
    if (['available', 'completed', 'attended', 'present', 'checkedin'].includes(s)) return 'success';
    if (['full', 'cancelled', 'absent', 'noshow'].includes(s)) return 'danger';
    if (['inprogress', 'pending'].includes(s)) return 'warning';
    if (['scheduled', 'reserved', 'booked', 'active'].includes(s)) return 'primary';
    return 'neutral';
  }

  linkFor(kind: AgendaItemKind): string {
    if (kind === 'exam') return '/school/theory-exams';
    if (kind === 'practical') return '/school/practical';
    return '/school/training';
  }
}
