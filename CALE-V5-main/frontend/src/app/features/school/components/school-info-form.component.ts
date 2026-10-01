import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';

export interface SchoolInfo {
  legalName: string;
  taxId: string;
  billingEmail: string;
  phone: string;
  address: string;
  city: string;
  department: string;
}

const NOT_REGISTERED = 'Sin registrar';

export const COLOMBIA_DEPARTMENTS = [
  'Amazonas', 'Antioquia', 'Arauca', 'Atlántico', 'Bogotá D.C.', 'Bolívar', 'Boyacá', 'Caldas',
  'Caquetá', 'Casanare', 'Cauca', 'Cesar', 'Chocó', 'Córdoba', 'Cundinamarca', 'Guainía',
  'Guaviare', 'Huila', 'La Guajira', 'Magdalena', 'Meta', 'Nariño', 'Norte de Santander',
  'Putumayo', 'Quindío', 'Risaralda', 'San Andrés y Providencia', 'Santander', 'Sucre',
  'Tolima', 'Valle del Cauca', 'Vaupés', 'Vichada'
];

/** Basic school data (name, NIT, contact and location). PUTs to the given endpoint. */
@Component({
  selector: 'app-school-info-form',
  standalone: true,
  imports: [ReactiveFormsModule, UiButtonComponent],
  template: `
    <form class="form" [formGroup]="form" (ngSubmit)="save()">
      <label class="field full">
        Nombre de la escuela
        <input formControlName="legalName" autocomplete="organization" />
      </label>
      <label class="field">
        NIT
        <input formControlName="taxId" inputmode="numeric" />
      </label>
      <label class="field">
        Teléfono
        <input formControlName="phone" type="tel" autocomplete="tel" />
      </label>
      <label class="field full">
        Dirección
        <input formControlName="address" autocomplete="street-address" />
      </label>
      <label class="field">
        Ciudad
        <input formControlName="city" autocomplete="address-level2" placeholder="Ej: Barranquilla" />
      </label>
      <label class="field">
        Departamento
        <select formControlName="department">
          <option value="" disabled>Elige el departamento</option>
          @for (d of departments; track d) {
            <option [value]="d">{{ d }}</option>
          }
        </select>
      </label>
      <label class="field full">
        Correo de contacto
        <input formControlName="billingEmail" type="email" autocomplete="email" />
      </label>

      @if (error()) {
        <p class="msg error full" role="alert">{{ error() }}</p>
      }
      @if (ok()) {
        <p class="msg ok full" role="status">{{ ok() }}</p>
      }

      <div class="full actions">
        <ui-button type="submit" [loading]="saving()" [disabled]="form.invalid">Guardar datos</ui-button>
        @if (cancellable) {
          <ui-button type="button" variant="ghost" [disabled]="saving()" (click)="cancelled.emit()">Cancelar</ui-button>
        }
      </div>
    </form>
  `,
  styles: [`
    .form { display: grid; gap: 0.9rem; grid-template-columns: repeat(2, minmax(0, 1fr)); margin-top: 0.75rem; }
    .full { grid-column: 1 / -1; }
    .field { display: grid; gap: 0.4rem; font-weight: 700; font-size: 1rem; }
    .field input, .field select {
      min-height: 3rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      padding: 0.6rem 0.8rem;
      background: var(--color-surface);
      color: var(--color-text);
      font: inherit;
      font-weight: 500;
    }
    .field input:focus, .field select:focus {
      outline: none;
      border-color: var(--color-primary);
      box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-primary) 20%, transparent);
    }
    .msg { margin: 0; font-weight: 600; }
    .msg.error { color: var(--color-danger); }
    .msg.ok { color: var(--color-success, var(--color-primary)); }
    .actions { display: flex; flex-wrap: wrap; gap: 0.6rem; }
    @media (max-width: 600px) {
      .form { grid-template-columns: minmax(0, 1fr); }
      .actions ui-button { width: 100%; }
    }
  `]
})
export class SchoolInfoFormComponent implements OnChanges {
  private readonly fb = inject(FormBuilder);
  private readonly http = inject(HttpClient);

  @Input({ required: true }) info!: SchoolInfo;
  /** API path, e.g. `/api/school/billing` or `/api/admin/schools/4/billing`. */
  @Input({ required: true }) endpoint!: string;
  @Input() cancellable = false;
  @Output() saved = new EventEmitter<SchoolInfo>();
  @Output() cancelled = new EventEmitter<void>();

  readonly departments = COLOMBIA_DEPARTMENTS;
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    legalName: ['', [Validators.required, Validators.maxLength(250)]],
    taxId: ['', [Validators.required, Validators.maxLength(32)]],
    phone: ['', [Validators.maxLength(40)]],
    address: ['', [Validators.maxLength(300)]],
    city: ['', [Validators.required, Validators.maxLength(120)]],
    department: ['', [Validators.required]],
    billingEmail: ['', [Validators.required, Validators.email]]
  });

  ngOnChanges(): void {
    if (!this.info) {
      return;
    }
    const clean = (v: string | null | undefined) => (!v || v === NOT_REGISTERED ? '' : v);
    this.form.reset({
      legalName: this.info.legalName ?? '',
      taxId: this.info.taxId === 'PENDIENTE' ? '' : this.info.taxId ?? '',
      phone: clean(this.info.phone),
      address: clean(this.info.address),
      city: clean(this.info.city),
      department: matchDepartment(clean(this.info.department)),
      billingEmail: this.info.billingEmail ?? ''
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.ok.set(null);
    this.http.put<SchoolInfo>(`${env.apiUrl}${this.endpoint}`, {
      legalName: v.legalName.trim(),
      taxId: v.taxId.trim(),
      billingEmail: v.billingEmail.trim(),
      phone: v.phone.trim(),
      address: v.address.trim(),
      city: v.city.trim(),
      department: v.department
    }).subscribe({
      next: (dto) => {
        this.saving.set(false);
        this.ok.set('Datos guardados.');
        this.saved.emit(dto);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}

/** Maps free-text department values (e.g. "atlantico") onto the list; unknown values become empty. */
function matchDepartment(value: string): string {
  const norm = (s: string) => s.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();
  const target = norm(value);
  if (!target) {
    return '';
  }
  if (target === 'bogota' || target === 'bogota dc' || target === 'bogota d.c.') {
    return 'Bogotá D.C.';
  }
  return COLOMBIA_DEPARTMENTS.find((d) => norm(d) === target) ?? '';
}
