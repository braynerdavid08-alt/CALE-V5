import { Component, computed, input } from '@angular/core';
import { NgStyle } from '@angular/common';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import {
  EditorSlide,
  ImageProps,
  LineProps,
  SLIDE_H,
  SLIDE_W,
  ShapeKind,
  ShapeProps,
  SlideElement,
  TextProps,
  VideoProps,
  backgroundCss,
  hasImageCrop,
  imageElementStyles,
  shapeClipPath
} from './presentation.models';

@Component({
  selector: 'app-slide-preview',
  standalone: true,
  imports: [NgStyle],
  templateUrl: './slide-preview.component.html',
  styleUrl: './slide-preview.component.css'
})
export class SlidePreviewComponent {
  readonly slide = input.required<EditorSlide>();
  /** Ancho visual del preview (px). Alto = 16:9. */
  readonly width = input(168);

  readonly slideW = SLIDE_W;
  readonly slideH = SLIDE_H;
  readonly media = resolveMediaUrl;

  readonly scale = computed(() => this.width() / SLIDE_W);
  readonly shellH = computed(() => Math.round(this.width() * (SLIDE_H / SLIDE_W)));

  bgStyle(slide: EditorSlide): Record<string, string> {
    const css = backgroundCss(slide.background);
    if (css['backgroundImage'] && slide.background.imageUrl) {
      return {
        ...css,
        backgroundImage: `url(${resolveMediaUrl(slide.background.imageUrl)})`
      };
    }
    return css;
  }

  textProps(el: SlideElement): TextProps {
    return el.props as TextProps;
  }

  imageProps(el: SlideElement): ImageProps {
    return el.props as ImageProps;
  }

  videoProps(el: SlideElement): VideoProps {
    return el.props as VideoProps;
  }

  shapeProps(el: SlideElement): ShapeProps {
    return el.props as ShapeProps;
  }

  lineProps(el: SlideElement): LineProps {
    return el.props as LineProps;
  }

  imageHasCrop(el: SlideElement): boolean {
    return el.type === 'image' && hasImageCrop((el.props as ImageProps).crop);
  }

  imageStyles(el: SlideElement): Record<string, string> {
    if (el.type !== 'image') {
      return {};
    }
    return imageElementStyles(el.props as ImageProps);
  }

  shapeClip(shape: ShapeKind): string | null {
    return shapeClipPath(shape);
  }
}
