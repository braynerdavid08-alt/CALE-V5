import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiStatComponent } from '../../../shared/ui/ui-stat.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import {
  SchoolCommercialApi,
  SchoolCommercialPostDto,
  SchoolCommercialProfileDto,
  SchoolCommercialPromotionDto,
  mediaIdFromUrl
} from '../school-commercial.api';

@Component({
  selector: 'app-school-commercial-page',
  standalone: true,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    UiButtonComponent,
    UiCardComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent,
    UiStatComponent,
    UiSuccessComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Escuela"
      title="Mi presencia digital"
      subtitle="Ficha comercial del directorio público. Solo aparece si tienes plan mensual/anual activo, pago confirmado y autorizas la publicación." />

    <ui-error [message]="error()" />
    <ui-success [message]="success()" />

    @if (loading()) {
      <ui-loading />
    } @else if (profile()) {
      @if (profile()!.isCommerciallySuspended) {
        <ui-card>
          <h2>Presencia suspendida</h2>
          <p>{{ profile()!.commercialSuspensionReason || 'Luz Verde suspendió la vitrina comercial.' }}</p>
        </ui-card>
      }

      <div class="stats">
        <ui-stat label="Vistas (30 d)" [value]="'' + (profile()!.stats?.pageViews30d ?? 0)" />
        <ui-stat label="WhatsApp (30 d)" [value]="'' + (profile()!.stats?.whatsAppClicks30d ?? 0)" />
        <ui-stat label="Mapas (30 d)" [value]="'' + (profile()!.stats?.mapsClicks30d ?? 0)" />
        <ui-stat label="Llamadas (30 d)" [value]="'' + (profile()!.stats?.phoneClicks30d ?? 0)" />
      </div>

      <ui-card>
        <div class="head-row">
          <div>
            <h2>Estado</h2>
            <p class="muted">
              {{ profile()!.isPublished ? 'Publicada' : 'No publicada' }}
              · ficha {{ profile()!.isSheetComplete ? 'completa' : 'incompleta' }}
              · elegible {{ profile()!.presenceEligible ? 'sí' : 'no' }}
              @if (profile()!.presenceDenyReason) {
                ({{ profile()!.presenceDenyReason }})
              }
            </p>
            @if (profile()!.publicPath) {
              <p><a [routerLink]="profile()!.publicPath" target="_blank" rel="noopener">Ver ficha pública</a></p>
            }
          </div>
          <div class="actions">
            <ui-button type="button" variant="secondary" (click)="setPublished(false)" [disabled]="busy() || !profile()!.isPublished">
              Ocultar
            </ui-button>
            <ui-button type="button" (click)="setPublished(true)" [disabled]="busy() || profile()!.isPublished || profile()!.isCommerciallySuspended">
              Publicar
            </ui-button>
          </div>
        </div>
        <p class="legal-note">
          Al publicar autorizas que datos de contacto y descripción de tu CEA aparezcan en el directorio público.
          El sistema legal completo de consentimientos versionados sigue pendiente de definición jurídica.
        </p>
      </ui-card>

      <ui-card>
        <h2>Ficha comercial</h2>
        <form class="form" [formGroup]="form" (ngSubmit)="save()">
          <label>
            Descripción (mín. 40 caracteres)
            <textarea rows="5" formControlName="about" maxlength="1200"></textarea>
          </label>
          <label>
            Categorías de licencia
            <input formControlName="licenseCategories" placeholder="A2, B1" maxlength="120" />
          </label>
          <label>
            Slug público
            <input formControlName="slug" placeholder="mi-cea" maxlength="80" />
          </label>
          <div class="grid2">
            <label>
              WhatsApp (solo dígitos)
              <input formControlName="whatsApp" placeholder="573001234567" maxlength="32" />
            </label>
            <label>
              Correo público
              <input formControlName="publicEmail" type="email" maxlength="320" />
            </label>
          </div>
          <div class="media-row">
            <div>
              <p class="label">Portada</p>
              <input type="file" accept="image/*" (change)="onCover($event)" />
              @if (form.value.coverMediaId) {
                <p class="muted">Imagen lista</p>
              }
            </div>
            <div>
              <p class="label">Logo</p>
              <input type="file" accept="image/*" (change)="onLogo($event)" />
              @if (form.value.logoMediaId) {
                <p class="muted">Imagen lista</p>
              }
            </div>
          </div>
          <ui-button type="submit" [disabled]="busy() || form.invalid || profile()!.isCommerciallySuspended">
            Guardar ficha
          </ui-button>
        </form>
      </ui-card>

      <ui-card>
        <h2>Publicaciones</h2>
        <form class="form" [formGroup]="postForm" (ngSubmit)="addPost()">
          <label>
            Nueva publicación
            <textarea rows="3" formControlName="body" maxlength="1500"></textarea>
          </label>
          <ui-button type="submit" [disabled]="busy() || postForm.invalid">Agregar borrador</ui-button>
        </form>
        <ul class="list">
          @for (post of posts(); track post.id) {
            <li>
              <p>{{ post.body }}</p>
              <p class="muted">{{ post.status }} · {{ post.updatedAt | date:'short' }}</p>
              <div class="actions">
                <ui-button type="button" variant="secondary" (click)="togglePost(post)" [disabled]="busy()">
                  {{ post.status === 'Published' ? 'Ocultar' : 'Publicar' }}
                </ui-button>
                <ui-button type="button" variant="ghost" (click)="removePost(post)" [disabled]="busy()">Eliminar</ui-button>
              </div>
            </li>
          }
        </ul>
      </ui-card>

      <ui-card>
        <h2>Promociones</h2>
        <form class="form" [formGroup]="promoForm" (ngSubmit)="addPromo()">
          <label>
            Título
            <input formControlName="title" maxlength="120" />
          </label>
          <label>
            Texto
            <textarea rows="3" formControlName="body" maxlength="800"></textarea>
          </label>
          <div class="grid2">
            <label>
              Desde
              <input type="date" formControlName="startsOn" />
            </label>
            <label>
              Hasta
              <input type="date" formControlName="endsOn" />
            </label>
          </div>
          <ui-button type="submit" [disabled]="busy() || promoForm.invalid">Agregar borrador</ui-button>
        </form>
        <ul class="list">
          @for (promo of promos(); track promo.id) {
            <li>
              <strong>{{ promo.title }}</strong>
              <p>{{ promo.body }}</p>
              <p class="muted">{{ promo.startsOn }} → {{ promo.endsOn }} · {{ promo.status }}</p>
              <div class="actions">
                <ui-button type="button" variant="secondary" (click)="togglePromo(promo)" [disabled]="busy()">
                  {{ promo.status === 'Published' ? 'Ocultar' : 'Publicar' }}
                </ui-button>
                <ui-button type="button" variant="ghost" (click)="removePromo(promo)" [disabled]="busy()">Eliminar</ui-button>
              </div>
            </li>
          }
        </ul>
      </ui-card>
    }
  `,
  styles: [`
    .stats {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: 0.75rem;
      margin-bottom: 1rem;
    }
    .head-row {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      flex-wrap: wrap;
      align-items: flex-start;
    }
    .actions { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .form { display: grid; gap: 0.85rem; }
    label, .label {
      display: grid;
      gap: 0.35rem;
      font-size: var(--text-sm);
      font-weight: 700;
    }
    input, textarea {
      width: 100%;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md, 0.7rem);
      padding: 0.65rem 0.75rem;
      background: var(--color-surface);
      color: inherit;
      font: inherit;
    }
    .grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .media-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .muted { color: var(--color-text-secondary); margin: 0.25rem 0 0; font-size: var(--text-sm); font-weight: 500; }
    .legal-note {
      margin: 0.85rem 0 0;
      color: var(--color-text-secondary);
      font-size: var(--text-xs);
      line-height: 1.45;
      max-width: 42rem;
    }
    .list { list-style: none; padding: 0; margin: 1rem 0 0; display: grid; gap: 0.85rem; }
    .list li {
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md, 0.7rem);
      padding: 0.85rem;
    }
    h2 { margin: 0 0 0.75rem; font-size: var(--text-md); }
    @media (max-width: 800px) {
      .stats, .grid2, .media-row { grid-template-columns: 1fr; }
    }
  `]
})
export class SchoolCommercialPage implements OnInit {
  private readonly api = inject(SchoolCommercialApi);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly profile = signal<SchoolCommercialProfileDto | null>(null);
  readonly posts = signal<SchoolCommercialPostDto[]>([]);
  readonly promos = signal<SchoolCommercialPromotionDto[]>([]);

  readonly form = this.fb.nonNullable.group({
    about: ['', [Validators.required, Validators.minLength(40), Validators.maxLength(1200)]],
    licenseCategories: ['', [Validators.required, Validators.maxLength(120)]],
    slug: ['', [Validators.maxLength(80)]],
    whatsApp: [''],
    publicEmail: [''],
    coverMediaId: [null as string | null],
    logoMediaId: [null as string | null]
  });

  readonly postForm = this.fb.nonNullable.group({
    body: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(1500)]]
  });

  readonly promoForm = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(120)]],
    body: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(800)]],
    startsOn: ['', Validators.required],
    endsOn: ['', Validators.required]
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getProfile().subscribe({
      next: (p) => {
        this.profile.set(p);
        this.form.patchValue({
          about: p.about,
          licenseCategories: p.licenseCategories,
          slug: p.slug ?? '',
          whatsApp: p.whatsApp ?? '',
          publicEmail: p.publicEmail ?? '',
          coverMediaId: p.coverMediaId,
          logoMediaId: p.logoMediaId
        });
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
    this.api.listPosts().subscribe({
      next: (rows) => this.posts.set(rows),
      error: () => this.posts.set([])
    });
    this.api.listPromotions().subscribe({
      next: (rows) => this.promos.set(rows),
      error: () => this.promos.set([])
    });
  }

  save(): void {
    if (this.form.invalid) return;
    this.busy.set(true);
    this.error.set(null);
    this.success.set(null);
    const v = this.form.getRawValue();
    this.api.saveProfile({
      about: v.about,
      licenseCategories: v.licenseCategories,
      coverMediaId: v.coverMediaId,
      logoMediaId: v.logoMediaId,
      whatsApp: v.whatsApp || null,
      publicEmail: v.publicEmail || null,
      slug: v.slug || null
    }).subscribe({
      next: (p) => {
        this.profile.set(p);
        this.success.set('Ficha comercial guardada.');
        this.busy.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.busy.set(false);
      }
    });
  }

  setPublished(publish: boolean): void {
    this.busy.set(true);
    this.error.set(null);
    this.api.setPublication(publish).subscribe({
      next: (p) => {
        this.profile.set(p);
        this.success.set(publish ? 'Ficha publicada en el directorio.' : 'Ficha ocultada del directorio.');
        this.busy.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.busy.set(false);
      }
    });
  }

  onCover(ev: Event): void {
    this.upload(ev, 'coverMediaId');
  }

  onLogo(ev: Event): void {
    this.upload(ev, 'logoMediaId');
  }

  private upload(ev: Event, field: 'coverMediaId' | 'logoMediaId'): void {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.busy.set(true);
    this.api.uploadMedia(file).subscribe({
      next: (res) => {
        this.form.patchValue({ [field]: mediaIdFromUrl(res.url) });
        this.busy.set(false);
        this.success.set('Imagen subida.');
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.busy.set(false);
      }
    });
  }

  addPost(): void {
    if (this.postForm.invalid) return;
    this.busy.set(true);
    this.api.createPost(this.postForm.controls.body.value, null).subscribe({
      next: (row) => {
        this.posts.set([row, ...this.posts()]);
        this.postForm.reset({ body: '' });
        this.busy.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.busy.set(false);
      }
    });
  }

  togglePost(post: SchoolCommercialPostDto): void {
    this.busy.set(true);
    this.api.setPostPublication(post.id, post.status !== 'Published').subscribe({
      next: (row) => {
        this.posts.set(this.posts().map((p) => (p.id === row.id ? row : p)));
        this.busy.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.busy.set(false);
      }
    });
  }

  removePost(post: SchoolCommercialPostDto): void {
    this.busy.set(true);
    this.api.deletePost(post.id).subscribe({
      next: () => {
        this.posts.set(this.posts().filter((p) => p.id !== post.id));
        this.busy.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.busy.set(false);
      }
    });
  }

  addPromo(): void {
    if (this.promoForm.invalid) return;
    const v = this.promoForm.getRawValue();
    this.busy.set(true);
    this.api.createPromotion({
      title: v.title,
      body: v.body,
      mediaId: null,
      startsOn: v.startsOn,
      endsOn: v.endsOn
    }).subscribe({
      next: (row) => {
        this.promos.set([row, ...this.promos()]);
        this.promoForm.reset({ title: '', body: '', startsOn: '', endsOn: '' });
        this.busy.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.busy.set(false);
      }
    });
  }

  togglePromo(promo: SchoolCommercialPromotionDto): void {
    this.busy.set(true);
    this.api.setPromotionPublication(promo.id, promo.status !== 'Published').subscribe({
      next: (row) => {
        this.promos.set(this.promos().map((p) => (p.id === row.id ? row : p)));
        this.busy.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.busy.set(false);
      }
    });
  }

  removePromo(promo: SchoolCommercialPromotionDto): void {
    this.busy.set(true);
    this.api.deletePromotion(promo.id).subscribe({
      next: () => {
        this.promos.set(this.promos().filter((p) => p.id !== promo.id));
        this.busy.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.busy.set(false);
      }
    });
  }
}
