export interface PublicShopProductDto {
  id: number;
  name: string;
  slug: string;
  shortDescription: string;
  description: string | null;
  imageUrl: string | null;
  imageUrls: string[];
  priceCop: number;
  compareAtPriceCop: number | null;
  promoLabel: string | null;
  discountPercent: number | null;
  category: string;
  productType: string | null;
  brand: string | null;
  reference: string | null;
  sizes: string | null;
  colors: string | null;
  stock: number;
  isFeatured: boolean;
  hasPromo: boolean;
  createdAt: string;
}

export interface PublicShopDto {
  pageTitle: string;
  pageSubtitle: string;
  heroImageUrl: string | null;
  ctaLabel: string;
  whatsApp: string | null;
  canOrder: boolean;
  products: PublicShopProductDto[];
}

export interface ShopSettingsDto {
  pageTitle: string;
  pageSubtitle: string;
  heroImageUrl: string | null;
  whatsApp: string | null;
  ctaLabel: string;
  updatedAt: string;
}

export interface AdminShopProductDto {
  id: number;
  name: string;
  slug: string;
  shortDescription: string;
  description: string | null;
  imageUrl: string | null;
  imageUrls: string[];
  priceCop: number;
  compareAtPriceCop: number | null;
  promoLabel: string | null;
  category: string;
  productType: string | null;
  brand: string | null;
  reference: string | null;
  sizes: string | null;
  colors: string | null;
  stock: number;
  isActive: boolean;
  isFeatured: boolean;
  sortOrder: number;
  createdAt: string;
  updatedAt: string;
}

export interface AdminShopDto {
  settings: ShopSettingsDto;
  platformWhatsAppFallback: string | null;
  products: AdminShopProductDto[];
}

export interface UpdateShopSettingsRequest {
  pageTitle: string;
  pageSubtitle: string;
  heroImageUrl: string | null;
  whatsApp: string | null;
  ctaLabel: string;
}

export interface UpsertShopProductRequest {
  name: string;
  shortDescription: string;
  description: string | null;
  imageUrl: string | null;
  imageUrls: string[];
  priceCop: number;
  compareAtPriceCop: number | null;
  promoLabel: string | null;
  category: string | null;
  productType: string | null;
  brand: string | null;
  reference: string | null;
  sizes: string | null;
  colors: string | null;
  stock: number;
  isActive: boolean;
  isFeatured: boolean;
  sortOrder: number;
}

export const SHOP_CATEGORIES = [
  'Cascos',
  'Guantes',
  'Chaquetas',
  'Chalecos reflectivos',
  'Impermeables',
  'Visores',
  'Accesorios'
] as const;

export const SHOP_HELMET_TYPES = [
  'Integral',
  'Abatible',
  'Abierto',
  'Modular',
  'Infantil'
] as const;
