import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { mapApiError } from '../../../core/http/map-api-error';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { ShopApi } from '../../public/shop.api';
import {
  AdminShopDto,
  AdminShopProductDto,
  UpdateShopSettingsRequest,
  UpsertShopProductRequest
} from '../../public/shop.models';

@Component({
  selector: 'app-admin-shop-page',
  standalone: true,
  imports: [
    CurrencyPipe,
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
      title="Cascos y equipo"
      subtitle="Edita el catálogo público, precios, promociones e imágenes. Los pedidos salen por WhatsApp." />

    <ui-error [message]="error()" />
    <ui-success [message]="success()" />

    @if (loading()) {
      <ui-loading label="Cargando tienda…" />
    } @else {
      @if (model(); as m) {
      <form class="stack" (ngSubmit)="saveSettings()">
        <section class="panel">
          <h2>Página pública</h2>
          <div class="grid-2">
            <label class="field">Título
              <input [(ngModel)]="m.settings.pageTitle" name="pageTitle" />
            </label>
            <label class="field">Texto del botón
              <input [(ngModel)]="m.settings.ctaLabel" name="ctaLabel" />
            </label>
            <label class="field full">Subtítulo
              <textarea rows="2" [(ngModel)]="m.settings.pageSubtitle" name="pageSubtitle"></textarea>
            </label>
            <label class="field">WhatsApp de pedidos (solo dígitos, ej. 573001234567)
              <input [(ngModel)]="m.settings.whatsApp" name="whatsApp" placeholder="57300…" />
            </label>
            <label class="field">URL imagen hero
              <input [(ngModel)]="m.settings.heroImageUrl" name="heroImageUrl" />
            </label>
          </div>
          @if (m.platformWhatsAppFallback) {
            <p class="hint">
              Si dejas el WhatsApp vacío, se usa el de la plataforma:
              {{ m.platformWhatsAppFallback }}.
            </p>
          } @else {
            <p class="hint">Configura un WhatsApp aquí o en Payments:School para activar los pedidos.</p>
          }
          <div class="upload-row">
            <label class="file-btn">
              Subir imagen hero
              <input type="file" accept="image/*" (change)="onUploadSettings($event)" hidden />
            </label>
            @if (uploading() === 'settings') {
              <span class="hint">Subiendo…</span>
            }
          </div>
          @if (m.settings.heroImageUrl) {
            <img class="preview" [src]="media(m.settings.heroImageUrl)" alt="Vista previa hero" />
          }
          <div class="actions">
            <ui-button type="submit" [disabled]="savingSettings()">
              {{ savingSettings() ? 'Guardando…' : 'Guardar página' }}
            </ui-button>
          </div>
        </section>
      </form>

      <section class="panel">
        <div class="panel-head">
          <h2>Productos ({{ m.products.length }})</h2>
          <ui-button type="button" variant="secondary" (click)="startNew()">Añadir producto</ui-button>
        </div>

        @if (editing(); as draft) {
          <form class="editor" (ngSubmit)="saveProduct()">
            <h3>{{ draft.id ? 'Editar producto' : 'Nuevo producto' }}</h3>
            <div class="grid-2">
              <label class="field">Nombre
                <input [(ngModel)]="draft.name" name="name" required />
              </label>
              <label class="field">Categoría
                <input [(ngModel)]="draft.category" name="category" placeholder="Cascos" />
              </label>
              <label class="field">Tipo (ej. Integral)
                <input [(ngModel)]="draft.productType" name="productType" placeholder="Integral, Abatible…" />
              </label>
              <label class="field">Marca
                <input [(ngModel)]="draft.brand" name="brand" placeholder="HJC, LS2…" />
              </label>
              <label class="field">Referencia
                <input [(ngModel)]="draft.reference" name="reference" placeholder="C10" />
              </label>
              <label class="field full">Descripción corta
                <textarea rows="2" [(ngModel)]="draft.shortDescription" name="shortDescription" required></textarea>
              </label>
              <label class="field full">Descripción larga
                <textarea rows="3" [(ngModel)]="draft.description" name="description"></textarea>
              </label>
              <label class="field">Precio COP
                <input type="number" [(ngModel)]="draft.priceCop" name="priceCop" min="1000" step="1000" />
              </label>
              <label class="field">Precio tachado (promo)
                <input type="number" [(ngModel)]="draft.compareAtPriceCop" name="compareAt" min="0" step="1000" />
              </label>
              <label class="field">Etiqueta promo
                <input [(ngModel)]="draft.promoLabel" name="promoLabel" placeholder="20% OFF" />
              </label>
              <label class="field">Tallas
                <input [(ngModel)]="draft.sizes" name="sizes" placeholder="S, M, L, XL" />
              </label>
              <label class="field">Colores
                <input [(ngModel)]="draft.colors" name="colors" placeholder="Negro, Rojo/Negro" />
              </label>
              <label class="field">Stock
                <input type="number" [(ngModel)]="draft.stock" name="stock" min="0" />
              </label>
              <label class="field">Orden
                <input type="number" [(ngModel)]="draft.sortOrder" name="sortOrder" min="0" />
              </label>
            </div>
            <div class="checks">
              <label><input type="checkbox" [(ngModel)]="draft.isActive" name="isActive" /> Visible en la tienda</label>
              <label><input type="checkbox" [(ngModel)]="draft.isFeatured" name="isFeatured" /> Destacado</label>
            </div>
            <div class="gallery-block">
              <div class="panel-head">
                <h3>Fotos (hasta 8 ángulos)</h3>
                <label class="file-btn">
                  Añadir fotos
                  <input type="file" accept="image/*" multiple (change)="onUploadProduct($event)" hidden />
                </label>
              </div>
              @if (uploading() === 'product') {
                <span class="hint">Subiendo…</span>
              }
              <div class="gallery">
                @for (url of draft.imageUrls; track url; let i = $index) {
                  <figure class="gallery-item" [class.cover]="i === 0">
                    <img [src]="media(url)" [alt]="'Foto ' + (i + 1)" />
                    <div class="gallery-actions">
                      @if (i !== 0) {
                        <button type="button" (click)="makeCover(i)">Portada</button>
                      } @else {
                        <span class="cover-tag">Portada</span>
                      }
                      <button type="button" class="danger" (click)="removeImage(i)">Quitar</button>
                    </div>
                  </figure>
                } @empty {
                  <p class="hint">Sube varias fotos del mismo casco (frente, lado, interior…).</p>
                }
              </div>
            </div>
            <div class="item-actions">
              <ui-button type="submit" [disabled]="savingProduct()">
                {{ savingProduct() ? 'Guardando…' : 'Guardar producto' }}
              </ui-button>
              <ui-button type="button" variant="secondary" (click)="cancelEdit()">Cancelar</ui-button>
            </div>
          </form>
        }

        <div class="list">
          @for (p of m.products; track p.id) {
            <article class="row">
              <div class="thumb">
                @if (p.imageUrl) {
                  <img [src]="media(p.imageUrl)" [alt]="p.name" />
                } @else {
                  <span>—</span>
                }
              </div>
              <div class="meta">
                <strong>{{ p.name }}</strong>
                <span>{{ p.category }} · {{ p.priceCop | currency:'COP':'symbol-narrow':'1.0-0' }}
                  @if (p.promoLabel) { · {{ p.promoLabel }} }
                </span>
                <span class="flags">
                  {{ p.isActive ? 'Visible' : 'Oculto' }}
                  @if (p.isFeatured) { · Destacado }
                  · Stock {{ p.stock }}
                </span>
              </div>
              <div class="row-actions">
                <ui-button type="button" variant="secondary" (click)="edit(p)">Editar</ui-button>
                <ui-button type="button" variant="danger" (click)="remove(p)">Eliminar</ui-button>
              </div>
            </article>
          } @empty {
            <p class="hint">Aún no hay productos. Añade hasta unos 10 cascos o accesorios.</p>
          }
        </div>
      </section>
      }
    }
  `,
  styles: [`
    .stack { display: grid; gap: 1rem; }
    .panel {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 1.1rem 1.15rem 1.25rem;
      box-shadow: var(--shadow-sm);
    }
    .panel h2 { margin: 0 0 0.85rem; font-size: var(--text-md); }
    .panel-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 0.85rem;
    }
    .panel-head h2 { margin: 0; }
    .grid-2 {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.75rem 1rem;
    }
    .field {
      display: grid;
      gap: 0.3rem;
      font-size: var(--text-sm);
      font-weight: 600;
      color: var(--color-text-secondary);
    }
    .field.full { grid-column: 1 / -1; }
    .field input, .field textarea, .field select {
      font: inherit;
      font-weight: 500;
      color: var(--color-text);
      background: var(--color-bg);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      padding: 0.55rem 0.7rem;
    }
    .checks {
      display: flex;
      flex-wrap: wrap;
      gap: 0.85rem 1.25rem;
      margin: 0.75rem 0;
      font-size: var(--text-sm);
    }
    .hint { margin: 0.55rem 0 0; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .upload-row { display: flex; align-items: center; gap: 0.75rem; margin-top: 0.75rem; }
    .file-btn {
      display: inline-flex;
      align-items: center;
      padding: 0.45rem 0.8rem;
      border: 1px dashed var(--color-border);
      border-radius: var(--radius-md);
      cursor: pointer;
      font-size: var(--text-sm);
      font-weight: 700;
    }
    .preview {
      margin-top: 0.75rem;
      max-width: min(420px, 100%);
      border-radius: var(--radius-md);
      border: 1px solid var(--color-border);
    }
    .gallery-block { margin-top: 0.85rem; }
    .gallery-block h3 { margin: 0; font-size: var(--text-sm); }
    .gallery {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
      gap: 0.65rem;
      margin-top: 0.65rem;
    }
    .gallery-item {
      margin: 0;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      overflow: hidden;
      background: #101812;
    }
    .gallery-item.cover { border-color: color-mix(in srgb, var(--color-primary) 55%, var(--color-border)); }
    .gallery-item img { width: 100%; aspect-ratio: 4/3; object-fit: cover; display: block; }
    .gallery-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.25rem;
      padding: 0.35rem;
      background: var(--color-surface);
    }
    .gallery-actions button, .cover-tag {
      font: inherit;
      font-size: 0.7rem;
      font-weight: 700;
      border: 0;
      background: transparent;
      color: var(--color-text-secondary);
      cursor: pointer;
      padding: 0.15rem 0.25rem;
    }
    .gallery-actions .danger { color: #b42318; }
    .cover-tag { color: var(--color-primary); cursor: default; }
    .actions, .item-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.55rem;
      margin-top: 0.9rem;
    }
    .editor {
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      padding: 0.9rem;
      margin-bottom: 1rem;
      background: color-mix(in srgb, var(--color-bg) 70%, var(--color-surface));
    }
    .editor h3 { margin: 0 0 0.75rem; font-size: var(--text-sm); }
    .list { display: grid; gap: 0.65rem; }
    .row {
      display: grid;
      grid-template-columns: 72px 1fr auto;
      gap: 0.75rem;
      align-items: center;
      padding: 0.55rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
    }
    .thumb {
      width: 72px;
      height: 54px;
      border-radius: 0.4rem;
      overflow: hidden;
      background: #101812;
      display: grid;
      place-items: center;
      color: rgba(255,255,255,0.4);
      font-size: 0.75rem;
    }
    .thumb img { width: 100%; height: 100%; object-fit: cover; }
    .meta { display: grid; gap: 0.15rem; }
    .meta strong { font-size: var(--text-sm); }
    .meta span { color: var(--color-text-secondary); font-size: var(--text-xs); }
    .row-actions { display: flex; gap: 0.4rem; flex-wrap: wrap; justify-content: flex-end; }
    @media (max-width: 720px) {
      .grid-2 { grid-template-columns: 1fr; }
      .row { grid-template-columns: 64px 1fr; }
      .row-actions { grid-column: 1 / -1; }
    }
  `]
})
export class AdminShopPage implements OnInit {
  private readonly api = inject(ShopApi);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly model = signal<AdminShopDto | null>(null);
  readonly editing = signal<(UpsertShopProductRequest & { id?: number }) | null>(null);
  readonly maxImages = 8;
  readonly savingSettings = signal(false);
  readonly savingProduct = signal(false);
  readonly uploading = signal<'settings' | 'product' | null>(null);

  ngOnInit(): void {
    this.reload();
  }

  media(path: string | null | undefined): string {
    return resolveMediaUrl(path);
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getAdmin().subscribe({
      next: (data) => {
        this.model.set(structuredClone(data));
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  saveSettings(): void {
    const m = this.model();
    if (!m) {
      return;
    }
    const body: UpdateShopSettingsRequest = {
      pageTitle: m.settings.pageTitle,
      pageSubtitle: m.settings.pageSubtitle,
      heroImageUrl: m.settings.heroImageUrl,
      whatsApp: m.settings.whatsApp,
      ctaLabel: m.settings.ctaLabel
    };
    this.savingSettings.set(true);
    this.error.set(null);
    this.success.set(null);
    this.api.saveSettings(body).subscribe({
      next: (data) => {
        this.model.set(structuredClone(data));
        this.success.set('Página de la tienda guardada.');
        this.savingSettings.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.savingSettings.set(false);
      }
    });
  }

  startNew(): void {
    this.editing.set({
      name: '',
      shortDescription: '',
      description: null,
      imageUrl: null,
      imageUrls: [],
      priceCop: 180000,
      compareAtPriceCop: null,
      promoLabel: null,
      category: 'Cascos',
      productType: 'Integral',
      brand: null,
      reference: null,
      sizes: 'S, M, L, XL',
      colors: null,
      stock: 5,
      isActive: true,
      isFeatured: false,
      sortOrder: (this.model()?.products.length ?? 0) + 1
    });
    this.success.set(null);
  }

  edit(p: AdminShopProductDto): void {
    const urls = p.imageUrls?.length
      ? [...p.imageUrls]
      : p.imageUrl
        ? [p.imageUrl]
        : [];
    this.editing.set({
      id: p.id,
      name: p.name,
      shortDescription: p.shortDescription,
      description: p.description,
      imageUrl: urls[0] ?? null,
      imageUrls: urls,
      priceCop: p.priceCop,
      compareAtPriceCop: p.compareAtPriceCop,
      promoLabel: p.promoLabel,
      category: p.category,
      productType: p.productType,
      brand: p.brand,
      reference: p.reference,
      sizes: p.sizes,
      colors: p.colors,
      stock: p.stock,
      isActive: p.isActive,
      isFeatured: p.isFeatured,
      sortOrder: p.sortOrder
    });
    this.success.set(null);
  }

  cancelEdit(): void {
    this.editing.set(null);
  }

  saveProduct(): void {
    const draft = this.editing();
    if (!draft) {
      return;
    }
    const imageUrls = (draft.imageUrls ?? []).filter(Boolean);
    const body: UpsertShopProductRequest = {
      name: draft.name,
      shortDescription: draft.shortDescription,
      description: draft.description || null,
      imageUrl: imageUrls[0] ?? null,
      imageUrls,
      priceCop: Number(draft.priceCop),
      compareAtPriceCop: draft.compareAtPriceCop ? Number(draft.compareAtPriceCop) : null,
      promoLabel: draft.promoLabel || null,
      category: draft.category || 'Cascos',
      productType: draft.productType || null,
      brand: draft.brand || null,
      reference: draft.reference || null,
      sizes: draft.sizes || null,
      colors: draft.colors || null,
      stock: Number(draft.stock),
      isActive: draft.isActive,
      isFeatured: draft.isFeatured,
      sortOrder: Number(draft.sortOrder)
    };
    this.savingProduct.set(true);
    this.error.set(null);
    this.success.set(null);
    const req = draft.id
      ? this.api.updateProduct(draft.id, body)
      : this.api.createProduct(body);
    req.subscribe({
      next: () => {
        this.editing.set(null);
        this.savingProduct.set(false);
        this.success.set('Producto guardado.');
        this.reload();
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.savingProduct.set(false);
      }
    });
  }

  remove(p: AdminShopProductDto): void {
    if (!confirm(`¿Eliminar «${p.name}» del catálogo?`)) {
      return;
    }
    this.error.set(null);
    this.api.deleteProduct(p.id).subscribe({
      next: () => {
        this.success.set('Producto eliminado.');
        if (this.editing()?.id === p.id) {
          this.editing.set(null);
        }
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  onUploadSettings(event: Event): void {
    this.upload(event, 'settings', (url) => {
      const m = this.model();
      if (m) {
        m.settings.heroImageUrl = url;
        this.model.set({ ...m });
      }
    });
  }

  onUploadProduct(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    if (!files.length) {
      return;
    }
    const draft = this.editing();
    if (!draft) {
      return;
    }
    const room = this.maxImages - (draft.imageUrls?.length ?? 0);
    if (room <= 0) {
      this.error.set(`Máximo ${this.maxImages} fotos por producto.`);
      input.value = '';
      return;
    }
    const queue = files.slice(0, room);
    this.uploading.set('product');
    this.error.set(null);
    this.uploadMany(queue, 0, [], (urls) => {
      const current = this.editing();
      if (!current) {
        return;
      }
      const imageUrls = [...(current.imageUrls ?? []), ...urls];
      this.editing.set({
        ...current,
        imageUrls,
        imageUrl: imageUrls[0] ?? null
      });
      this.uploading.set(null);
      input.value = '';
    }, (err) => {
      this.error.set(mapApiError(err));
      this.uploading.set(null);
      input.value = '';
    });
  }

  removeImage(index: number): void {
    const draft = this.editing();
    if (!draft) {
      return;
    }
    const imageUrls = (draft.imageUrls ?? []).filter((_, i) => i !== index);
    this.editing.set({
      ...draft,
      imageUrls,
      imageUrl: imageUrls[0] ?? null
    });
  }

  makeCover(index: number): void {
    const draft = this.editing();
    if (!draft?.imageUrls?.length || index <= 0) {
      return;
    }
    const imageUrls = [...draft.imageUrls];
    const [picked] = imageUrls.splice(index, 1);
    imageUrls.unshift(picked);
    this.editing.set({ ...draft, imageUrls, imageUrl: imageUrls[0] });
  }

  private upload(
    event: Event,
    kind: 'settings' | 'product',
    apply: (url: string) => void
  ): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    this.uploading.set(kind);
    this.error.set(null);
    this.api.uploadMedia(file).subscribe({
      next: (res) => {
        apply(res.url);
        this.uploading.set(null);
        input.value = '';
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.uploading.set(null);
        input.value = '';
      }
    });
  }

  private uploadMany(
    files: File[],
    index: number,
    done: string[],
    ok: (urls: string[]) => void,
    fail: (err: unknown) => void
  ): void {
    if (index >= files.length) {
      ok(done);
      return;
    }
    this.api.uploadMedia(files[index]).subscribe({
      next: (res) => this.uploadMany(files, index + 1, [...done, res.url], ok, fail),
      error: fail
    });
  }
}
