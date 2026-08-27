export interface PublicHeroDto {
  visible: boolean;
  badge: string;
  title: string;
  titleHighlight: string;
  description: string;
  ctaPrimaryLabel: string;
  ctaPrimaryPath: string;
  ctaSecondaryLabel: string;
  videoUrl: string | null;
  imageUrl: string | null;
  imageUrlMobile: string | null;
  imageAlt: string;
  imageEnabled: boolean;
}

export interface HomepageBenefitItem {
  id: string;
  title: string;
  description: string;
  icon: string;
  tone: string;
  sortOrder: number;
  active: boolean;
}

export interface HomepageStepItem {
  id: string;
  number: number;
  title: string;
  description: string;
  icon: string;
  tone: string;
  sortOrder: number;
  active: boolean;
}

export interface ResolvedStatDto {
  key: string;
  label: string;
  subLabel: string;
  icon: string;
  mode: string;
  manualValue: string | null;
  lastComputedValue: string | null;
  lastComputedDisplay: string | null;
  displayValue: string | null;
  source: string;
  visible: boolean;
  sortOrder: number;
  lastComputedAt: string | null;
  updatedAt: string;
}

export interface PublicSchoolCardDto {
  id: number;
  name: string;
  city: string;
  department: string;
  detailPath: string;
}

export interface PublicInstructorCardDto {
  id: number;
  displayName: string;
  schoolName: string | null;
  detailPath: string;
}

export interface PublicHomeDto {
  hero: PublicHeroDto;
  benefits: HomepageBenefitItem[];
  stepsVisible: boolean;
  stepsTitle: string;
  stepsSubtitle: string;
  steps: HomepageStepItem[];
  stats: ResolvedStatDto[];
  schoolsVisible: boolean;
  schools: PublicSchoolCardDto[];
  instructorsVisible: boolean;
  instructors: PublicInstructorCardDto[];
  seoTitle: string;
  seoDescription: string;
  aboutHtml: string;
  blogIntro: string;
  contactEmail: string;
  contactPhone: string;
  updatedAt: string;
}

export interface AdminHomepageDto {
  heroBadge: string;
  heroTitle: string;
  heroTitleHighlight: string;
  heroDescription: string;
  heroCtaPrimaryLabel: string;
  heroCtaPrimaryPath: string;
  heroCtaSecondaryLabel: string;
  heroVideoUrl: string | null;
  heroImageUrl: string | null;
  heroImageUrlMobile: string | null;
  heroImageAlt: string;
  heroImageEnabled: boolean;
  heroVisible: boolean;
  benefitsSectionVisible: boolean;
  stepsSectionVisible: boolean;
  statsSectionVisible: boolean;
  schoolsSectionVisible: boolean;
  instructorsSectionVisible: boolean;
  stepsSectionTitle: string;
  stepsSectionSubtitle: string;
  benefits: HomepageBenefitItem[];
  steps: HomepageStepItem[];
  stats: ResolvedStatDto[];
  seoTitle: string;
  seoDescription: string;
  aboutHtml: string;
  blogIntro: string;
  contactEmail: string;
  contactPhone: string;
  updatedAt: string;
  updatedByUserId: number | null;
}

export interface UpdateHomepageStatRequest {
  key: string;
  label?: string | null;
  subLabel?: string | null;
  icon?: string | null;
  mode: string;
  manualValue?: string | null;
  visible: boolean;
  sortOrder: number;
  note?: string | null;
}

export interface UpdateHomepageRequest {
  heroBadge?: string | null;
  heroTitle?: string | null;
  heroTitleHighlight?: string | null;
  heroDescription?: string | null;
  heroCtaPrimaryLabel?: string | null;
  heroCtaPrimaryPath?: string | null;
  heroCtaSecondaryLabel?: string | null;
  heroVideoUrl?: string | null;
  heroImageUrl?: string | null;
  heroImageUrlMobile?: string | null;
  heroImageAlt?: string | null;
  heroImageEnabled: boolean;
  heroVisible: boolean;
  benefitsSectionVisible: boolean;
  stepsSectionVisible: boolean;
  statsSectionVisible: boolean;
  schoolsSectionVisible: boolean;
  instructorsSectionVisible: boolean;
  stepsSectionTitle?: string | null;
  stepsSectionSubtitle?: string | null;
  benefits?: HomepageBenefitItem[] | null;
  steps?: HomepageStepItem[] | null;
  stats?: UpdateHomepageStatRequest[] | null;
  seoTitle?: string | null;
  seoDescription?: string | null;
  aboutHtml?: string | null;
  blogIntro?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
  changeNote?: string | null;
}
