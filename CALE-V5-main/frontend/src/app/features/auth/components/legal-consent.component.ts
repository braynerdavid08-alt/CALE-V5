import { Component, Input } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LegalConsent } from '../../legal/legal.api';

/** Grupo de casillas del registro. Ninguna llega marcada; el servidor vuelve a validarlas. */
export function legalConsentGroup(fb: FormBuilder, school = false) {
  return fb.nonNullable.group({
    acceptTerms: [false, Validators.requiredTrue],
    acknowledgePrivacy: [false, Validators.requiredTrue],
    authorizeDataProcessing: [false, Validators.requiredTrue],
    acceptSchoolTerms: [false, school ? Validators.requiredTrue : []]
  });
}

export function consentValue(group: FormGroup): LegalConsent {
  return group.getRawValue() as LegalConsent;
}

@Component({
  selector: 'auth-legal-consent',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <fieldset class="consent" [formGroup]="group">
      <legend>Documentos legales</legend>
      <label class="check">
        <input type="checkbox" formControlName="acceptTerms" />
        <span>Acepto los <a href="/legal/terms" target="_blank" rel="noopener">Términos y Condiciones</a>.</span>
      </label>
      <label class="check">
        <input type="checkbox" formControlName="acknowledgePrivacy" />
        <span>He leído el <a href="/legal/privacy" target="_blank" rel="noopener">Aviso de Privacidad</a>.</span>
      </label>
      <label class="check">
        <input type="checkbox" formControlName="authorizeDataProcessing" />
        <span>
          Autorizo el tratamiento de mis datos personales según la
          <a href="/legal/data-processing" target="_blank" rel="noopener">Política de Tratamiento de Datos</a>.
        </span>
      </label>
      @if (school) {
        <label class="check">
          <input type="checkbox" formControlName="acceptSchoolTerms" />
          <span>Acepto las <a href="/legal/school-terms" target="_blank" rel="noopener">Condiciones para escuelas (CEA)</a>.</span>
        </label>
      }
      @if (group.touched && group.invalid) {
        <span class="field-error" role="alert">Marca cada casilla para crear la cuenta.</span>
      }
      <small class="note">Los documentos están en borrador y pueden cambiar; si cambian te pediremos aceptarlos de nuevo.</small>
    </fieldset>
  `,
  styles: [`
    .consent { border: 0; padding: 0; margin: 0.25rem 0 0.75rem; display: grid; gap: 0.45rem; }
    legend { font-weight: 600; font-size: 0.9rem; margin-bottom: 0.25rem; }
    .check { display: flex; gap: 0.5rem; align-items: flex-start; font-size: 0.86rem; line-height: 1.4; cursor: pointer; }
    .check input { margin-top: 0.2rem; width: 1rem; height: 1rem; flex: none; }
    .note { color: var(--color-text-secondary); font-size: 0.75rem; }
  `]
})
export class LegalConsentComponent {
  @Input({ required: true }) group!: FormGroup;
  @Input() school = false;
}
