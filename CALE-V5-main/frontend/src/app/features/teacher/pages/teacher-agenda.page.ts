import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { env } from '../../../core/config/env';
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
import {
  AgendaItemDto,
  AgendaItemKind,
  SchoolAgendaDto
} from '../../school/api/exam-schedule.api';
import { Tone, plural } from '../../school/utils/exam-labels';

type ViewMode = 'day' | 'week';
type Filter = 'all' | 'theory' | 'practical';

interface AgendaDay {
  date: string;
  title: string;
  isToday: boolean;
  items: AgendaItemDto[];
}

const KIND_LABELS: Record<string, string> = {
  theory: 'Clase teórica',
  practical: 'Clase de manejo'
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
  selector: 'app-teacher-agenda-page',
  standalone: true,
  imports: [RouterLink, UiErrorComponent, UiLoadingComponent],
  templateUrl: './teacher-agenda.page.html',
  styleUrls: ['../../../shared/styles/easy-schedule.css', './teacher-agenda.page.css']
})
export class TeacherAgendaPage implements OnInit {
  private readonly http = inject(HttpClient);

  readonly today = todayColombia();
  readonly view = signal<ViewMode>('week');
  readonly anchor = signal(mondayOf(this.today));
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<AgendaItemDto[]>([]);
  readonly filter = signal<Filter>('all');
  readonly expanded = signal<Set<string>>(new Set());

  readonly views: { value: ViewMode; label: string }[] = [
    { value: 'day', label: 'Día' },
    { value: 'week', label: 'Semana' }
  ];

  readonly filters: { value: Filter; label: string }[] = [
    { value: 'all', label: 'Todo' },
    { value: 'theory', label: 'Teoría' },
    { value: 'practical', label: 'Manejo' }
  ];

  readonly rangeFrom = computed(() =>
    this.view() === 'day' ? this.anchor() : mondayOf(this.anchor())
  );

  readonly rangeTo = computed(() =>
    this.view() === 'day' ? this.rangeFrom() : addDays(this.rangeFrom(), 6)
  );

  readonly rangeLabel = computed(() =>
    this.view() === 'day'
      ? dayTitle(this.rangeFrom())
      : weekRangeLabel(this.rangeFrom(), this.rangeTo())
  );

  readonly isCurrentRange = computed(() =>
    this.view() === 'day'
      ? this.rangeFrom() === this.today
      : this.rangeFrom() === mondayOf(this.today)
  );

  readonly days = computed<AgendaDay[]>(() => {
    const f = this.filter();
    const visible = this.items().filter((i) => f === 'all' || i.kind === f);
    const out: AgendaDay[] = [];
    let cursor = this.rangeFrom();
    const to = this.rangeTo();
    while (cursor <= to) {
      out.push({
        date: cursor,
        title: dayTitle(cursor),
        isToday: cursor === this.today,
        items: visible
          .filter((it) => it.date.slice(0, 10) === cursor)
          .sort((a, b) => hhmm(a.startTime).localeCompare(hhmm(b.startTime)))
      });
      cursor = addDays(cursor, 1);
    }
    return out;
  });

  readonly visibleCount = computed(() => this.days().reduce((n, d) => n + d.items.length, 0));

  readonly time12 = time12;
  readonly plural = plural;

  ngOnInit(): void {
    this.load();
  }

  setView(v: ViewMode): void {
    this.view.set(v);
    this.anchor.set(v === 'day' ? this.today : mondayOf(this.today));
    this.expanded.set(new Set());
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    let params = new HttpParams()
      .set('from', this.rangeFrom())
      .set('to', this.rangeTo());
    this.http.get<SchoolAgendaDto>(`${env.apiUrl}/api/teacher/agenda`, { params }).subscribe({
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

  go(offset: number): void {
    if (offset === 0) {
      this.setView(this.view());
      return;
    }
    if (this.view() === 'day') this.anchor.set(addDays(this.anchor(), offset));
    else this.anchor.set(addDays(mondayOf(this.anchor()), offset * 7));
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

  kindLabel(kind: AgendaItemKind | string): string {
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
}
