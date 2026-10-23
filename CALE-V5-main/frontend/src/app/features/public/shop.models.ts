export interface PublicShopProductDto {
  id: number;
  name: string;
  slug: string;
  shortDescription: string;
  description: string | null;
  imageUrl: string | null;
  priceCop: number;
  compareAtPriceCop: number | null;
  promoLabel: string | null;
  discountPercent: number | null;
  category: string;
  sizes: string | null;
  stock: number;
  isFeatured: boolean;
  hasPromo: boolean;
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
  priceCop: number;
  compareAtPriceCop: number | null;
  promoLabel: string | null;
  category: string;
  sizes: string | null;
  stock: number;
  isActive: boolean;
  isFeatured: boolean;
  sortOrder: number;
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
  priceCop: number;
  compareAtPriceCop: number | null;
  promoLabel: string | null;
  category: string | null;
  sizes: string | null;
  stock: number;
  isActive: boolean;
  isFeatured: boolean;
  sortOrder: number;
}
