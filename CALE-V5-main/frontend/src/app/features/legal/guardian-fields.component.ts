import { Component, Input } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { GUARDIAN_RELATIONSHIPS, Guardian, ageFromBirthDate } from './legal.api';

/** Datos mínimos del representante legal. El grupo se deshabilita mientras la persona sea mayor de edad. */
export function guardianGroup(fb: FormBuilder) {
  const group = fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(150)]],
    relationship: ['', Validators.required],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]]
  });
  group.disable();
  return group;
}

/** Habilita o deshabilita el grupo según la fecha de nacimiento; devuelve si es menor. */
export function syncGuardianGroup(group: FormGroup, birthDate: string): boolean {
  const age = ageFromBirthDate(birthDate);
  const minor = age !== null && age >= 0 && age < 18;
  if (minor && group.disabled) group.enable();
  if (!minor && group.enabled) group.disable();
  return minor;
}

export function guardianValue(group: FormGroup): Guardian | null {
  return group.enabled ? (group.getRawValue() as Guardian) : null;
}

@Component({
  selector: 'legal-guardian-fields',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <fieldset class="guardian" [formGroup]="group">
      <legend>Tu representante legal</legend>
      <p class="intro">
        Como eres menor de edad, tu madre, padre o tutor legal debe autorizar tu cuenta.
        Le enviaremos un correo para que lea cómo tratamos tus datos y decida.
      </p>
      <label class="field">
        Nombre completo del representante
        <input type="text" formControlName="name" autocomplete="off" maxlength="150" />
        @if (group.controls['name'].touched && group.controls['name'].invalid) {
          <span class="field-error" role="alert">Escribe su nombre completo.</span>
        }
      </label>
      <label class="field">
        Parentesco
        <select formControlName="relationship">
          <option value="" disabled>Elige una opción</option>
          @for (r of relationships; track r) {
            <option [value]="r">{{ r }}</option>
          }
        </select>
        @if (group.controls['relationship'].touched && group.controls['relationship'].invalid) {
          <span class="field-error" role="alert">Elige el parentesco.</span>
        }
      </label>
      <label class="field">
        Correo del representante
        <input type="email" formControlName="email" autocomplete="off" placeholder="correo@ejemplo.com" />
        @if (group.controls['email'].touched && group.controls['email'].invalid) {
          <span class="field-error" role="alert">Escribe un correo válido, distinto al tuyo.</span>
        }
      </label>
    </fieldset>
  `,
  styles: [`
    .guardian {
      display: grid;
      gap: 0.6rem;
      margin: 0.25rem 0 0.75rem;
      padding: 0.85rem 0.9rem;
      border: 1px solid color-mix(in srgb, var(--color-primary) 35%, var(--color-border));
      border-radius: var(--radius-md);
      background: color-mix(in srgb, var(--color-primary) 5%, transparent);
    }
    legend { padding: 0 0.3rem; font-weight: 700; font-size: 0.92rem; }
    .intro { margin: 0; font-size: 0.85rem; line-height: 1.45; color: var(--color-text-secondary); }
    .field input, .field select { min-height: 2.9rem; border-radius: 0.8rem; background: var(--color-background); font-size: 1rem; }
  `]
})
export class GuardianFieldsComponent {
  @Input({ required: true }) group!: FormGroup;
  readonly relationships = GUARDIAN_RELATIONSHIPS;
}
