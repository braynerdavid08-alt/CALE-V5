import { PublicShopDto, PublicShopProductDto } from './shop.models';

export function discountPercent(p: PublicShopProductDto): number {
  if (p.discountPercent != null && p.discountPercent > 0) return Math.round(p.discountPercent);
  if (p.compareAtPriceCop != null && p.compareAtPriceCop > p.priceCop && p.compareAtPriceCop > 0) {
    return Math.round(((p.compareAtPriceCop - p.priceCop) / p.compareAtPriceCop) * 100);
  }
  return 0;
}

export function discountLabel(p: PublicShopProductDto): string | null {
  const pct = discountPercent(p);
  return pct > 0 ? `−${pct}%` : null;
}

export function savingsCop(p: PublicShopProductDto): number | null {
  if (p.compareAtPriceCop != null && p.compareAtPriceCop > p.priceCop) {
    return p.compareAtPriceCop - p.priceCop;
  }
  return null;
}

export function productImages(p: PublicShopProductDto): string[] {
  if (p.imageUrls?.length) return p.imageUrls;
  return p.imageUrl ? [p.imageUrl] : [];
}

export function splitVariants(raw: string | null | undefined): string[] {
  if (!raw?.trim()) return [];
  return raw
    .split(/[,;/|]+/)
    .map((s) => s.trim())
    .filter(Boolean);
}

export function formatCop(value: number): string {
  return new Intl.NumberFormat('es-CO', {
    style: 'currency',
    currency: 'COP',
    maximumFractionDigits: 0
  }).format(value);
}

export function productPublicUrl(slug: string): string {
  if (typeof window === 'undefined') return `/tienda/${slug}`;
  return `${window.location.origin}/tienda/${slug}`;
}

export interface WhatsAppOrderOptions {
  size?: string | null;
  color?: string | null;
  quantity?: number | null;
}

/** Builds and opens a WhatsApp inquiry. Returns false if number is missing. */
export function openWhatsAppOrder(
  shop: Pick<PublicShopDto, 'whatsApp' | 'canOrder'>,
  product: PublicShopProductDto,
  options: WhatsAppOrderOptions = {}
): boolean {
  if (!shop.whatsApp || !shop.canOrder) return false;

  const lines = [
    'Hola, estoy interesado en un producto de Luz Verde.',
    '',
    `Producto: ${product.name}`,
    product.brand ? `Marca: ${product.brand}` : null,
    product.reference ? `Referencia: ${product.reference}` : null,
    `Precio anunciado: ${formatCop(product.priceCop)}`,
    options.color ? `Color: ${options.color}` : product.colors ? `Colores: ${product.colors}` : null,
    options.size ? `Talla: ${options.size}` : product.sizes ? `Tallas: ${product.sizes}` : null,
    options.quantity && options.quantity > 0 ? `Cantidad: ${options.quantity}` : null,
    `URL: ${productPublicUrl(product.slug)}`,
    '',
    '¿Me pueden confirmar disponibilidad, precio final y opciones de envío?'
  ].filter((line): line is string => line != null);

  const url = `https://wa.me/${shop.whatsApp}?text=${encodeURIComponent(lines.join('\n'))}`;
  window.open(url, '_blank', 'noopener');
  return true;
}

export type ShopSort =
  | 'discount'
  | 'priceAsc'
  | 'priceDesc'
  | 'newest'
  | 'featured';

export function sortProducts(list: PublicShopProductDto[], sort: ShopSort): PublicShopProductDto[] {
  const copy = [...list];
  switch (sort) {
    case 'discount':
      return copy.sort((a, b) => discountPercent(b) - discountPercent(a) || a.priceCop - b.priceCop);
    case 'priceAsc':
      return copy.sort((a, b) => a.priceCop - b.priceCop);
    case 'priceDesc':
      return copy.sort((a, b) => b.priceCop - a.priceCop);
    case 'newest':
      return copy.sort((a, b) => +new Date(b.createdAt) - +new Date(a.createdAt));
    case 'featured':
      return copy.sort((a, b) => Number(b.isFeatured) - Number(a.isFeatured) || a.name.localeCompare(b.name));
    default:
      return copy;
  }
}
