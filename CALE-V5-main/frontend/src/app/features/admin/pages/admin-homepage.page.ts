import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { PublicHomeApi } from '../../public/public-home.api';
import {
  AdminHomepageDto,
  HomepageBenefitItem,
  HomepageStepItem,
  ResolvedStatDto,
  UpdateHomepageRequest
} from '../../public/public.models';

@Component({
  selector: 'app-admin-homepage-page',
  standalone: true,
  imports: [
    FormsModule,
    UiButtonComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent,
    UiSuccessComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Administración"
      title="Página de inicio"
      subtitle="Edita el contenido público de la landing CALE." />

    <ui-error [message]="error()" />
    <ui-success [message]="success()" />

    @if (loading()) {
      <ui-loading label="Cargando configuración..." />
    } @else {
      @if (model(); as m) {
      <form class="stack" (ngSubmit)="save()">
        <section class="panel">
          <h2>Hero</h2>
          <div class="checks">
            <label><input type="checkbox" [(ngModel)]="m.heroVisible" name="heroVisible" /> Visible</label>
            <label><input type="checkbox" [(ngModel)]="m.heroImageEnabled" name="heroImageEnabled" /> Imagen activa</label>
          </div>
          <div class="grid-2">
            <label class="field">Badge
              <input [(ngModel)]="m.heroBadge" name="heroBadge" />
            </label>
            <label class="field">Alt de imagen
              <input [(ngModel)]="m.heroImageAlt" name="heroImageAlt" />
            </label>
            <label class="field">Título
              <input [(ngModel)]="m.heroTitle" name="heroTitle" />
            </label>
            <label class="field">Resalte del título
              <input [(ngModel)]="m.heroTitleHighlight" name="heroTitleHighlight" />
            </label>
            <label class="field full">Descripción
              <textarea rows="3" [(ngModel)]="m.heroDescription" name="heroDescription"></textarea>
            </label>
            <label class="field">CTA primario (texto)
              <input [(ngModel)]="m.heroCtaPrimaryLabel" name="heroCtaPrimaryLabel" />
            </label>
            <label class="field">CTA primario (ruta)
              <input [(ngModel)]="m.heroCtaPrimaryPath" name="heroCtaPrimaryPath" />
            </label>
            <label class="field">CTA secundario (texto)
              <input [(ngModel)]="m.heroCtaSecondaryLabel" name="heroCtaSecondaryLabel" />
            </label>
            <label class="field">URL video
              <input [(ngModel)]="m.heroVideoUrl" name="heroVideoUrl" placeholder="https://..." />
            </label>
            <label class="field">URL imagen
              <input [(ngModel)]="m.heroImageUrl" name="heroImageUrl" />
            </label>
            <label class="field">URL imagen móvil
              <input [(ngModel)]="m.heroImageUrlMobile" name="heroImageUrlMobile" />
            </label>
          </div>
          <div class="upload-row">
            <label class="file-btn">
              Subir imagen hero
              <input type="file" accept="image/*" (change)="onUpload($event, 'desktop')" hidden />
            </label>
            <label class="file-btn">
              Subir imagen móvil
              <input type="file" accept="image/*" (change)="onUpload($event, 'mobile')" hidden />
            </label>
            @if (uploading()) {
              <span class="hint">Subiendo…</span>
            }
          </div>
          @if (m.heroImageUrl) {
            <img class="preview" [src]="media(m.heroImageUrl)" alt="Vista previa hero" />
          }
        </section>

        <section class="panel">
          <h2>Secciones visibles</h2>
          <div class="checks">
            <label><input type="checkbox" [(ngModel)]="m.benefitsSectionVisible" name="benefitsSectionVisible" /> Beneficios</label>
            <label><input type="checkbox" [(ngModel)]="m.stepsSectionVisible" name="stepsSectionVisible" /> Pasos</label>
            <label><input type="checkbox" [(ngModel)]="m.statsSectionVisible" name="statsSectionVisible" /> Estadísticas</label>
            <label><input type="checkbox" [(ngModel)]="m.schoolsSectionVisible" name="schoolsSectionVisible" /> Escuelas</label>
            <label><input type="checkbox" [(ngModel)]="m.instructorsSectionVisible" name="instructorsSectionVisible" /> Instructores</label>
          </div>
        </section>

        <section class="panel">
          <div class="panel-head">
            <h2>Beneficios</h2>
            <ui-button type="button" variant="secondary" (click)="addBenefit()">Añadir</ui-button>
          </div>
          @for (b of m.benefits; track b.id; let i = $index) {
            <div class="item">
              <div class="grid-2">
                <label class="field">Título
                  <input [(ngModel)]="b.title" [name]="'bTitle' + i" />
                </label>
                <label class="field">Icono
                  <input [(ngModel)]="b.icon" [name]="'bIcon' + i" />
                </label>
                <label class="field">Tono
                  <select [(ngModel)]="b.tone" [name]="'bTone' + i">
                    <option value="blue">blue</option>
                    <option value="green">green</option>
                    <option value="purple">purple</option>
                    <option value="yellow">yellow</option>
                  </select>
                </label>
                <label class="field">Orden
                  <input type="number" [(ngModel)]="b.sortOrder" [name]="'bOrder' + i" />
                </label>
                <label class="field full">Descripción
                  <textarea rows="2" [(ngModel)]="b.description" [name]="'bDesc' + i"></textarea>
                </label>
              </div>
              <div class="item-actions">
                <label><input type="checkbox" [(ngModel)]="b.active" [name]="'bActive' + i" /> Activo</label>
                <ui-button type="button" variant="danger" (click)="removeBenefit(i)">Quitar</ui-button>
              </div>
            </div>
          }
        </section>

        <section class="panel">
          <h2>Cómo funciona</h2>
          <div class="grid-2">
            <label class="field">Título de sección
              <input [(ngModel)]="m.stepsSectionTitle" name="stepsSectionTitle" />
            </label>
            <label class="field">Subtítulo
              <input [(ngModel)]="m.stepsSectionSubtitle" name="stepsSectionSubtitle" />
            </label>
          </div>
          <div class="panel-head" style="margin-top: 1rem;">
            <h3>Pasos</h3>
            <ui-button type="button" variant="secondary" (click)="addStep()">Añadir</ui-button>
          </div>
          @for (s of m.steps; track s.id; let i = $index) {
            <div class="item">
              <div class="grid-2">
                <label class="field">Número
                  <input type="number" [(ngModel)]="s.number" [name]="'sNum' + i" />
                </label>
                <label class="field">Orden
                  <input type="number" [(ngModel)]="s.sortOrder" [name]="'sOrder' + i" />
                </label>
                <label class="field">Título
                  <input [(ngModel)]="s.title" [name]="'sTitle' + i" />
                </label>
                <label class="field">Icono
                  <input [(ngModel)]="s.icon" [name]="'sIcon' + i" />
                </label>
                <label class="field">Tono
                  <select [(ngModel)]="s.tone" [name]="'sTone' + i">
                    <option value="blue">blue</option>
                    <option value="green">green</option>
                    <option value="purple">purple</option>
                    <option value="yellow">yellow</option>
                  </select>
                </label>
                <label class="field full">Descripción
                  <textarea rows="2" [(ngModel)]="s.description" [name]="'sDesc' + i"></textarea>
                </label>
              </div>
              <div class="item-actions">
                <label><input type="checkbox" [(ngModel)]="s.active" [name]="'sActive' + i" /> Activo</label>
                <ui-button type="button" variant="danger" (click)="removeStep(i)">Quitar</ui-button>
              </div>
            </div>
          }
        </section>

        <section class="panel">
          <h2>Estadísticas</h2>
          <p class="hint">
            En Auto el valor lo calcula el servidor. En Manual usa el valor que indiques.
            La landing solo muestra <code>displayValue</code> resuelto; no inventes cifras.
          </p>
          @for (st of m.stats; track st.key; let i = $index) {
            <div class="item">
              <div class="stat-key">{{ st.key }}</div>
              <div class="grid-2">
                <label class="field">Etiqueta
                  <input [(ngModel)]="st.label" [name]="'stLabel' + i" />
                </label>
                <label class="field">Subetiqueta
                  <input [(ngModel)]="st.subLabel" [name]="'stSub' + i" />
                </label>
                <label class="field">Icono
                  <input [(ngModel)]="st.icon" [name]="'stIcon' + i" />
                </label>
                <label class="field">Modo
                  <select [(ngModel)]="st.mode" [name]="'stMode' + i">
                    <option value="Auto">Auto</option>
                    <option value="Manual">Manual</option>
                  </select>
                </label>
                <label class="field">Valor manual
                  <input [(ngModel)]="st.manualValue" [name]="'stManual' + i" [disabled]="st.mode !== 'Manual'" />
                </label>
                <label class="field">Orden
                  <input type="number" [(ngModel)]="st.sortOrder" [name]="'stOrder' + i" />
                </label>
              </div>
              <div class="item-actions">
                <label><input type="checkbox" [(ngModel)]="st.visible" [name]="'stVis' + i" /> Visible</label>
                <span class="hint">Actual: {{ st.displayValue || '—' }}</span>
              </div>
            </div>
          }
        </section>

        <section class="panel">
          <h2>SEO y textos</h2>
          <div class="grid-2">
            <label class="field">SEO título
              <input [(ngModel)]="m.seoTitle" name="seoTitle" />
            </label>
            <label class="field">SEO descripción
              <input [(ngModel)]="m.seoDescription" name="seoDescription" />
            </label>
            <label class="field">Correo contacto
              <input [(ngModel)]="m.contactEmail" name="contactEmail" />
            </label>
            <label class="field">Teléfono contacto
              <input [(ngModel)]="m.contactPhone" name="contactPhone" />
            </label>
            <label class="field full">Nosotros (HTML permitido)
              <textarea rows="4" [(ngModel)]="m.aboutHtml" name="aboutHtml"></textarea>
            </label>
            <label class="field full">Intro blog
              <textarea rows="3" [(ngModel)]="m.blogIntro" name="blogIntro"></textarea>
            </label>
            <label class="field full">Nota de cambio (auditoría)
              <input [(ngModel)]="changeNote" name="changeNote" />
            </label>
          </div>
        </section>

        <div class="save-row">
          <ui-button type="submit" [loading]="saving()">Guardar cambios</ui-button>
        </div>
      </form>
      }
    }
  `,
  styles: [`
    .stack { display: grid; gap: 1.1rem; padding-bottom: 2rem; }
    .panel {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 1.15rem 1.25rem;
      box-shadow: var(--shadow-sm);
    }
    .panel h2 { margin: 0 0 0.85rem; font-size: var(--text-lg); }
    .panel h3 { margin: 0; font-size: var(--text-md); }
    .panel-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 0.75rem;
    }
    .grid-2 {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.75rem;
    }
    .field {
      display: grid;
      gap: 0.3rem;
      font-size: var(--text-sm);
      font-weight: 600;
    }
    .field.full { grid-column: 1 / -1; }
    .field input,
    .field textarea,
    .field select {
      font: inherit;
      font-weight: 500;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      padding: 0.55rem 0.7rem;
      background: var(--color-surface-raised);
      color: var(--color-text);
    }
    .checks {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem 1.1rem;
      margin-bottom: 0.85rem;
      font-size: var(--text-sm);
      font-weight: 600;
    }
    .item {
      border-top: 1px solid var(--color-border);
      padding-top: 0.9rem;
      margin-top: 0.9rem;
    }
    .item-actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.75rem;
      margin-top: 0.65rem;
    }
    .stat-key {
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--color-primary);
      margin-bottom: 0.5rem;
    }
    .hint {
      margin: 0 0 0.75rem;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
      font-weight: 500;
    }
    .upload-row {
      display: flex;
      flex-wrap: wrap;
      gap: 0.65rem;
      align-items: center;
      margin-top: 0.85rem;
    }
    .file-btn {
      display: inline-flex;
      align-items: center;
      min-height: var(--control-height);
      padding: 0 0.9rem;
      border-radius: var(--radius-sm);
      border: 1px solid var(--color-border);
      background: var(--color-chip);
      font-size: var(--text-sm);
      font-weight: 700;
      cursor: pointer;
    }
    .preview {
      margin-top: 0.85rem;
      max-width: 280px;
      border-radius: var(--radius-md);
      border: 1px solid var(--color-border);
    }
    .save-row { display: flex; justify-content: flex-end; }
    @media (max-width: 700px) {
      .grid-2 { grid-template-columns: 1fr; }
    }
  `]
})
export class AdminHomepagePage implements OnInit {
  private readonly api = inject(PublicHomeApi);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly uploading = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly model = signal<AdminHomepageDto | null>(null);
  changeNote = '';

  ngOnInit(): void {
    this.api.getAdminHomepage().subscribe({
      next: (data) => {
        this.model.set(this.clone(data));
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  media(path: string | null | undefined): string {
    return resolveMediaUrl(path);
  }

  addBenefit(): void {
    const m = this.model();
    if (!m) {
      return;
    }
    const item: HomepageBenefitItem = {
      id: crypto.randomUUID().replace(/-/g, ''),
      title: 'Nuevo beneficio',
      description: '',
      icon: 'book',
      tone: 'blue',
      sortOrder: m.benefits.length + 1,
      active: true
    };
    m.benefits = [...m.benefits, item];
    this.model.set({ ...m });
  }

  removeBenefit(index: number): void {
    const m = this.model();
    if (!m) {
      return;
    }
    m.benefits = m.benefits.filter((_, i) => i !== index);
    this.model.set({ ...m });
  }

  addStep(): void {
    const m = this.model();
    if (!m) {
      return;
    }
    const item: HomepageStepItem = {
      id: crypto.randomUUID().replace(/-/g, ''),
      number: m.steps.length + 1,
      title: 'Nuevo paso',
      description: '',
      icon: 'users',
      tone: 'blue',
      sortOrder: m.steps.length + 1,
      active: true
    };
    m.steps = [...m.steps, item];
    this.model.set({ ...m });
  }

  removeStep(index: number): void {
    const m = this.model();
    if (!m) {
      return;
    }
    m.steps = m.steps.filter((_, i) => i !== index);
    this.model.set({ ...m });
  }

  onUpload(event: Event, target: 'desktop' | 'mobile'): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    this.error.set(null);
    this.uploading.set(true);
    this.api.uploadMedia(file).subscribe({
      next: (res) => {
        const m = this.model();
        if (m) {
          if (target === 'desktop') {
            m.heroImageUrl = res.url;
          } else {
            m.heroImageUrlMobile = res.url;
          }
          this.model.set({ ...m });
        }
        this.uploading.set(false);
        input.value = '';
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.uploading.set(false);
        input.value = '';
      }
    });
  }

  save(): void {
    const m = this.model();
    if (!m) {
      return;
    }
    this.error.set(null);
    this.success.set(null);
    this.saving.set(true);

    const body: UpdateHomepageRequest = {
      heroBadge: m.heroBadge,
      heroTitle: m.heroTitle,
      heroTitleHighlight: m.heroTitleHighlight,
      heroDescription: m.heroDescription,
      heroCtaPrimaryLabel: m.heroCtaPrimaryLabel,
      heroCtaPrimaryPath: m.heroCtaPrimaryPath,
      heroCtaSecondaryLabel: m.heroCtaSecondaryLabel,
      heroVideoUrl: m.heroVideoUrl,
      heroImageUrl: m.heroImageUrl,
      heroImageUrlMobile: m.heroImageUrlMobile,
      heroImageAlt: m.heroImageAlt,
      heroImageEnabled: m.heroImageEnabled,
      heroVisible: m.heroVisible,
      benefitsSectionVisible: m.benefitsSectionVisible,
      stepsSectionVisible: m.stepsSectionVisible,
      statsSectionVisible: m.statsSectionVisible,
      schoolsSectionVisible: m.schoolsSectionVisible,
      instructorsSectionVisible: m.instructorsSectionVisible,
      stepsSectionTitle: m.stepsSectionTitle,
      stepsSectionSubtitle: m.stepsSectionSubtitle,
      benefits: m.benefits,
      steps: m.steps,
      stats: m.stats.map((st: ResolvedStatDto) => ({
        key: st.key,
        label: st.label,
        subLabel: st.subLabel,
        icon: st.icon,
        mode: st.mode,
        manualValue: st.manualValue,
        visible: st.visible,
        sortOrder: st.sortOrder
      })),
      seoTitle: m.seoTitle,
      seoDescription: m.seoDescription,
      aboutHtml: m.aboutHtml,
      blogIntro: m.blogIntro,
      contactEmail: m.contactEmail,
      contactPhone: m.contactPhone,
      changeNote: this.changeNote || null
    };

    this.api.saveAdminHomepage(body).subscribe({
      next: (data) => {
        this.model.set(this.clone(data));
        this.success.set('Página de inicio guardada.');
        this.saving.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.saving.set(false);
      }
    });
  }

  private clone(data: AdminHomepageDto): AdminHomepageDto {
    return structuredClone(data);
  }
}
