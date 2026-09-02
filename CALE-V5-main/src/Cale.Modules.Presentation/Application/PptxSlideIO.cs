using System.Text.Json;
using Cale.Modules.Presentation.Application.Abstractions;
using Cale.Modules.Presentation.Application.DTOs;
using Cale.Modules.Presentation.Domain;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace Cale.Modules.Presentation.Application;

internal static class PptxSlideIO
{
    private const int CanvasW = 960;
    private const int CanvasH = 540;
    private const long DefaultSlideCx = 12_192_000L;
    private const long DefaultSlideCy = 6_858_000L;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Task<IReadOnlyList<ImportedSlideOutline>> ImportAsync(
        Stream stream,
        IPresentationMediaStore mediaStore,
        int? ownerId,
        CancellationToken ct = default)
    {
        using var presentation = PresentationDocument.Open(stream, false);
        var presentationPart = presentation.PresentationPart
            ?? throw new InvalidOperationException("El archivo PowerPoint no es válido.");

        var slideSize = presentationPart.Presentation?.SlideSize;
        var slideCx = (long)(slideSize?.Cx ?? DefaultSlideCx);
        var slideCy = (long)(slideSize?.Cy ?? DefaultSlideCy);
        var scaleX = CanvasW / (double)slideCx;
        var scaleY = CanvasH / (double)slideCy;

        var slideIds = presentationPart.Presentation?.SlideIdList?.Elements<P.SlideId>().ToList()
            ?? throw new InvalidOperationException("El PowerPoint no tiene diapositivas.");

        if (slideIds.Count == 0)
        {
            throw new InvalidOperationException("El PowerPoint no tiene diapositivas.");
        }

        var slides = new List<ImportedSlideOutline>();
        var index = 1;
        foreach (var slideId in slideIds)
        {
            var slidePart = (SlidePart)presentationPart.GetPartById(slideId.RelationshipId!);
            var tree = slidePart.Slide?.CommonSlideData?.ShapeTree;
            if (tree is null)
            {
                continue;
            }

            var collector = new SlideCollector(
                presentationPart,
                slidePart,
                mediaStore,
                ownerId,
                scaleX,
                scaleY,
                ct);
            collector.CollectSlide();

            var notes = ExtractSlideNotes(slidePart);
            var title = NormalizeSlideTitle(
                collector.Title ?? collector.TextBlocks.FirstOrDefault(),
                index);
            var body = collector.Body
                ?? string.Join(
                    "\n",
                    collector.TextBlocks
                        .Where(t => !string.Equals(t, title, StringComparison.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body) && collector.Elements.Count == 0)
            {
                continue;
            }

            var background = collector.BackgroundJson ?? PresentationTemplates.LightBackground;
            var elementsJson = BuildElementsJson(collector.Elements, title.Trim(), body.Trim());
            slides.Add(elementsJson is null
                ? new ImportedSlideOutline(title.Trim(), body.Trim(), notes, background, null)
                : new ImportedSlideOutline(title.Trim(), body.Trim(), notes, background, elementsJson));

            index++;
        }

        if (slides.Count == 0)
        {
            throw new InvalidOperationException(
                "No se pudo leer contenido del PowerPoint. Verifica que tenga texto o imágenes.");
        }

        return Task.FromResult<IReadOnlyList<ImportedSlideOutline>>(slides);
    }

    public static byte[] Export(
        PresentationDetailDto detail,
        Func<string, byte[]?>? resolveImageBytes = null)
    {
        using var ms = new MemoryStream();
        using (var doc = PresentationDocument.Create(ms, PresentationDocumentType.Presentation, true))
        {
            var presentationPart = doc.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation(
                new P.SlideIdList(),
                new P.SlideSize
                {
                    Cx = (int)DefaultSlideCx,
                    Cy = (int)DefaultSlideCy,
                    Type = P.SlideSizeValues.Screen16x9
                },
                new P.NotesSize { Cx = 6858000, Cy = 9144000 },
                new P.DefaultTextStyle());

            var slideMasterPart = CreateSlideMaster(presentationPart);
            var slideLayoutPart = CreateSlideLayout(slideMasterPart);
            var masterRelId = presentationPart.GetIdOfPart(slideMasterPart);
            presentationPart.Presentation.SlideMasterIdList = new P.SlideMasterIdList(
                new P.SlideMasterId { Id = 2147483648U, RelationshipId = masterRelId });

            var ordered = detail.Slides.OrderBy(s => s.Position).ToList();
            uint slideId = 256;
            foreach (var slide in ordered)
            {
                AddSlide(presentationPart, slideLayoutPart, slide, resolveImageBytes, ref slideId);
            }

            presentationPart.Presentation.Save();
        }

        return ms.ToArray();
    }

    private static void AddSlide(
        PresentationPart presentationPart,
        SlideLayoutPart slideLayoutPart,
        PresentationSlideDto slide,
        Func<string, byte[]?>? resolveImageBytes,
        ref uint slideId)
    {
        var slidePart = presentationPart.AddNewPart<SlidePart>();
        var relId = presentationPart.GetIdOfPart(slidePart);
        var slideIdList = presentationPart.Presentation!.SlideIdList!;
        slideIdList.Append(new P.SlideId { Id = slideId++, RelationshipId = relId });

        var shapeTree = new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new A.TransformGroup()));

        var z = 1U;
        var elements = ParseElements(slide.ElementsJson)
            .OrderBy(el => GetInt(el, "z", 0))
            .ToList();
        if (elements.Count == 0)
        {
            AppendTextShape(shapeTree, slide.Title, 64, 48, 832, 64, 32, true, ref z);
            AppendTextShape(shapeTree, ExtractPlainBody(slide.ElementsJson), 64, 140, 832, 320, 22, false, ref z);
        }
        else
        {
            foreach (var el in elements)
            {
                if (!el.TryGetProperty("type", out var typeProp))
                {
                    continue;
                }

                var type = typeProp.GetString();
                switch (type)
                {
                    case "text" when el.TryGetProperty("props", out var textProps):
                    {
                        var text = textProps.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                        var fontSize = textProps.TryGetProperty("fontSize", out var fs) ? fs.GetInt32() : 24;
                        var bold = textProps.TryGetProperty("fontWeight", out var fw) && fw.GetInt32() >= 600;
                        AppendTextShape(
                            shapeTree,
                            text,
                            GetInt(el, "x", 64),
                            GetInt(el, "y", 48),
                            GetInt(el, "w", 832),
                            GetInt(el, "h", 64),
                            fontSize,
                            bold,
                            ref z);
                        break;
                    }
                    case "image" when resolveImageBytes is not null:
                        AppendImagePicture(slidePart, shapeTree, el, resolveImageBytes, ref z);
                        break;
                    case "shape":
                        AppendFilledShape(shapeTree, el, ref z);
                        break;
                }
            }
        }

        slidePart.Slide = new P.Slide(
            new P.CommonSlideData(shapeTree),
            new P.ColorMapOverride(new A.MasterColorMapping()));

        slidePart.AddPart(slideLayoutPart);
        slidePart.Slide.Save();

        if (!string.IsNullOrWhiteSpace(slide.Notes))
        {
            var notesPart = slidePart.AddNewPart<NotesSlidePart>();
            var notesTree = new P.ShapeTree(
                new P.NonVisualGroupShapeProperties(
                    new P.NonVisualDrawingProperties { Id = 2U, Name = "" },
                    new P.NonVisualGroupShapeDrawingProperties(),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.GroupShapeProperties(new A.TransformGroup()));
            AppendTextShape(notesTree, slide.Notes, 40, 40, 600, 400, 14, false, ref z);
            notesPart.NotesSlide = new P.NotesSlide(
                new P.CommonSlideData(notesTree),
                new P.ColorMapOverride(new A.MasterColorMapping()));
            notesPart.NotesSlide.Save();
        }
    }

    private static void AppendImagePicture(
        SlidePart slidePart,
        OpenXmlCompositeElement tree,
        JsonElement el,
        Func<string, byte[]?> resolveImageBytes,
        ref uint shapeId)
    {
        if (!el.TryGetProperty("props", out var props))
        {
            return;
        }

        var src = props.TryGetProperty("src", out var srcProp) ? srcProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(src))
        {
            return;
        }

        var bytes = resolveImageBytes(src);
        if (bytes is null || bytes.Length == 0)
        {
            return;
        }

        var partType = GuessImagePartType(src, bytes);
        var imagePart = slidePart.AddImagePart(partType);
        using (var stream = new MemoryStream(bytes))
        {
            imagePart.FeedData(stream);
        }

        var embedId = slidePart.GetIdOfPart(imagePart);
        var cx = ToEmu(GetInt(el, "w", 400), CanvasW, DefaultSlideCx);
        var cy = ToEmu(GetInt(el, "h", 300), CanvasH, DefaultSlideCy);
        var offX = ToEmu(GetInt(el, "x", 64), CanvasW, DefaultSlideCx);
        var offY = ToEmu(GetInt(el, "y", 48), CanvasH, DefaultSlideCy);

        var picture = new P.Picture(
            new P.NonVisualPictureProperties(
                new P.NonVisualDrawingProperties { Id = shapeId++, Name = $"Image {shapeId}" },
                new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.BlipFill(
                new A.Blip { Embed = embedId, CompressionState = A.BlipCompressionValues.Print },
                new A.Stretch(new A.FillRectangle())),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = offX, Y = offY },
                    new A.Extents { Cx = cx, Cy = cy })));

        tree.Append(picture);
    }

    private static void AppendFilledShape(OpenXmlCompositeElement tree, JsonElement el, ref uint shapeId)
    {
        if (!el.TryGetProperty("props", out var props))
        {
            return;
        }

        var fill = props.TryGetProperty("fill", out var fillProp) ? fillProp.GetString() ?? "#2BB0ED" : "#2BB0ED";
        var stroke = props.TryGetProperty("stroke", out var strokeProp) ? strokeProp.GetString() ?? "#0B1F33" : "#0B1F33";
        var shapeKind = props.TryGetProperty("shape", out var shapeProp) ? shapeProp.GetString() : "rect";
        var isEllipse = string.Equals(shapeKind, "ellipse", StringComparison.OrdinalIgnoreCase);

        var cx = ToEmu(GetInt(el, "w", 200), CanvasW, DefaultSlideCx);
        var cy = ToEmu(GetInt(el, "h", 140), CanvasH, DefaultSlideCy);
        var offX = ToEmu(GetInt(el, "x", 64), CanvasW, DefaultSlideCx);
        var offY = ToEmu(GetInt(el, "y", 48), CanvasH, DefaultSlideCy);

        var shape = new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = shapeId++, Name = $"Shape {shapeId}" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = offX, Y = offY },
                    new A.Extents { Cx = cx, Cy = cy }),
                new A.PresetGeometry { Preset = isEllipse ? A.ShapeTypeValues.Ellipse : A.ShapeTypeValues.Rectangle }),
            new A.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph()));

        if (TryParseHexColor(fill, out var fillColor))
        {
            shape.ShapeProperties!.Append(new A.SolidFill(new A.RgbColorModelHex { Val = fillColor }));
        }

        if (TryParseHexColor(stroke, out var strokeColor))
        {
            shape.ShapeProperties!.Append(new A.Outline(
                new A.SolidFill(new A.RgbColorModelHex { Val = strokeColor }))
            { Width = 12700 });
        }

        tree.Append(shape);
    }

    private static ImagePartType GuessImagePartType(string src, byte[] bytes)
    {
        var lower = src.ToLowerInvariant();
        if (lower.EndsWith(".png") || (bytes.Length > 2 && bytes[0] == 0x89 && bytes[1] == 0x50))
        {
            return ImagePartType.Png;
        }

        if (lower.EndsWith(".gif") || (bytes.Length > 2 && bytes[0] == 0x47 && bytes[1] == 0x49))
        {
            return ImagePartType.Gif;
        }

        return ImagePartType.Jpeg;
    }

    private static bool TryParseHexColor(string? value, out string hex)
    {
        hex = "";
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var v = value.Trim().TrimStart('#');
        if (v.Length is 3 or 6 && v.All(Uri.IsHexDigit))
        {
            hex = v.Length == 3
                ? string.Concat(v.Select(c => $"{c}{c}"))
                : v.ToUpperInvariant();
            return true;
        }

        return false;
    }

    private static SlideMasterPart CreateSlideMaster(PresentationPart presentationPart)
    {
        var masterPart = presentationPart.AddNewPart<SlideMasterPart>();
        var layoutPart = masterPart.AddNewPart<SlideLayoutPart>();
        layoutPart.SlideLayout = new P.SlideLayout(
            new P.CommonSlideData(
                new P.ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new P.ApplicationNonVisualDrawingProperties()),
                    new P.GroupShapeProperties(new A.TransformGroup()))),
            new P.ColorMapOverride(new A.MasterColorMapping()))
        { Type = P.SlideLayoutValues.Blank, Preserve = true };
        layoutPart.SlideLayout.Save();

        masterPart.SlideMaster = new P.SlideMaster(
            new P.CommonSlideData(
                new P.ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new P.ApplicationNonVisualDrawingProperties()),
                    new P.GroupShapeProperties(new A.TransformGroup()))),
            new P.ColorMap(),
            new P.SlideLayoutIdList(new P.SlideLayoutId { Id = 2147483649U, RelationshipId = masterPart.GetIdOfPart(layoutPart) }),
            new P.TextStyles());
        masterPart.SlideMaster.Save();
        return masterPart;
    }

    private static SlideLayoutPart CreateSlideLayout(SlideMasterPart masterPart)
    {
        foreach (var part in masterPart.SlideLayoutParts)
        {
            return part;
        }

        throw new InvalidOperationException("No se pudo crear la plantilla de diapositiva.");
    }

    private static void AppendTextShape(
        OpenXmlCompositeElement tree,
        string text,
        int x,
        int y,
        int w,
        int h,
        int fontSize,
        bool bold,
        ref uint shapeId)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var cx = ToEmu(w, CanvasW, DefaultSlideCx);
        var cy = ToEmu(h, CanvasH, DefaultSlideCy);
        var offX = ToEmu(x, CanvasW, DefaultSlideCx);
        var offY = ToEmu(y, CanvasH, DefaultSlideCy);

        var body = new A.TextBody(
            new A.BodyProperties(),
            new A.ListStyle(),
            new A.Paragraph(new A.Run(
                bold ? new A.RunProperties { Language = "es-CO", FontSize = fontSize * 100, Bold = true }
                    : new A.RunProperties { Language = "es-CO", FontSize = fontSize * 100 },
                new A.Text(text))));

        var shape = new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = shapeId++, Name = $"Text {shapeId}" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = offX, Y = offY },
                    new A.Extents { Cx = cx, Cy = cy })),
            body);

        tree.Append(shape);
    }

    private static long ToEmu(int px, int canvas, long slideEmu) =>
        (long)Math.Round(px * (slideEmu / (double)canvas));

    private static List<JsonElement> ParseElements(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<JsonElement>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static int GetInt(JsonElement el, string name, int fallback) =>
        el.TryGetProperty(name, out var v) && v.TryGetInt32(out var n) ? n : fallback;

    private static string ExtractPlainBody(string elementsJson)
    {
        var parts = new List<string>();
        foreach (var el in ParseElements(elementsJson))
        {
            if (el.TryGetProperty("type", out var t) && t.GetString() == "text"
                && el.TryGetProperty("props", out var props)
                && props.TryGetProperty("text", out var text))
            {
                var value = text.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    parts.Add(value.Trim());
                }
            }
        }

        return parts.Count <= 1 ? parts.FirstOrDefault() ?? "" : string.Join("\n", parts.Skip(1));
    }

    private static string? ExtractSlideNotes(SlidePart slidePart)
    {
        var tree = slidePart.NotesSlidePart?.NotesSlide?.CommonSlideData?.ShapeTree;
        if (tree is null)
        {
            return null;
        }

        var texts = tree.Descendants<A.Text>()
            .Select(t => t.Text?.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t) && !IsNotesPlaceholder(t!))
            .ToList();

        return texts.Count == 0 ? null : string.Join("\n", texts);
    }

    private static bool IsNotesPlaceholder(string text) =>
        text.Contains("click to add notes", StringComparison.OrdinalIgnoreCase)
        || text.Contains("haga clic para agregar notas", StringComparison.OrdinalIgnoreCase);

    private static string? BuildElementsJson(List<object> elements, string title, string body)
    {
        if (elements.Count == 0)
        {
            return null;
        }

        var serialized = JsonSerializer.Serialize(elements, JsonOptions);
        var parsed = JsonSerializer.Deserialize<List<JsonElement>>(serialized, JsonOptions) ?? [];
        var clamped = parsed.Select(ClampElement).Where(HasElementContent).ToList();
        if (clamped.Count == 0 || !clamped.Any(IsOnCanvas))
        {
            return null;
        }

        if (!clamped.Any(HasReadableText))
        {
            var (_, _, overlayJson) = PresentationExchangeService.BuildSlideFromOutline(
                new ImportedSlideOutline(title, body, null));
            var overlay = JsonSerializer.Deserialize<List<JsonElement>>(overlayJson, JsonOptions) ?? [];
            clamped = overlay.Concat(clamped).ToList();
        }

        return JsonSerializer.Serialize(clamped, JsonOptions);
    }

    private static JsonElement ClampElement(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            return el;
        }

        var x = Math.Clamp(GetInt(el, "x", 0), 0, CanvasW - 40);
        var y = Math.Clamp(GetInt(el, "y", 0), 0, CanvasH - 40);
        var w = Math.Clamp(GetInt(el, "w", 120), 40, CanvasW - x);
        var h = Math.Clamp(GetInt(el, "h", 40), 24, CanvasH - y);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in el.EnumerateObject())
            {
                if (prop.NameEquals("x"))
                {
                    writer.WriteNumber("x", x);
                }
                else if (prop.NameEquals("y"))
                {
                    writer.WriteNumber("y", y);
                }
                else if (prop.NameEquals("w"))
                {
                    writer.WriteNumber("w", w);
                }
                else if (prop.NameEquals("h"))
                {
                    writer.WriteNumber("h", h);
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
    }

    private static bool HasElementContent(JsonElement el)
    {
        var type = el.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (type == "image")
        {
            return el.TryGetProperty("props", out var props)
                && props.TryGetProperty("src", out var src)
                && !string.IsNullOrWhiteSpace(src.GetString());
        }

        if (type == "text")
        {
            return el.TryGetProperty("props", out var props)
                && props.TryGetProperty("text", out var text)
                && !string.IsNullOrWhiteSpace(text.GetString());
        }

        return true;
    }

    private static bool IsOnCanvas(JsonElement el)
    {
        var x = GetInt(el, "x", 0);
        var y = GetInt(el, "y", 0);
        var w = GetInt(el, "w", 0);
        var h = GetInt(el, "h", 0);
        return x < CanvasW && y < CanvasH && x + w > 0 && y + h > 0;
    }

    private static bool HasReadableText(JsonElement el) =>
        el.TryGetProperty("type", out var t)
        && t.GetString() == "text"
        && el.TryGetProperty("props", out var props)
        && props.TryGetProperty("text", out var text)
        && !string.IsNullOrWhiteSpace(text.GetString());

    private sealed class SlideCollector
    {
        private readonly PresentationPart _presentationPart;
        private readonly SlidePart _slidePart;
        private readonly IPresentationMediaStore _mediaStore;
        private readonly int? _ownerId;
        private readonly CancellationToken _ct;
        private readonly double _scaleX;
        private readonly double _scaleY;
        private int _z = 1;
        private readonly HashSet<string> _savedVideoRelIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _imageUrlByRelId = new(StringComparer.Ordinal);

        public List<object> Elements { get; } = [];
        public List<string> TextBlocks { get; } = [];
        public string? Title { get; private set; }
        public string? Body { get; private set; }
        public string? BackgroundJson { get; private set; }

        public SlideCollector(
            PresentationPart presentationPart,
            SlidePart slidePart,
            IPresentationMediaStore mediaStore,
            int? ownerId,
            double scaleX,
            double scaleY,
            CancellationToken ct)
        {
            _presentationPart = presentationPart;
            _slidePart = slidePart;
            _mediaStore = mediaStore;
            _ownerId = ownerId;
            _scaleX = scaleX;
            _scaleY = scaleY;
            _ct = ct;
            BackgroundJson = TryReadBackground(slidePart);
        }

        public void CollectSlide()
        {
            var tree = _slidePart.Slide?.CommonSlideData?.ShapeTree;
            if (tree is not null)
            {
                CollectTree(tree, 0, 0);
            }

            if (TextBlocks.Count < 2)
            {
                var layoutTree = _slidePart.SlideLayoutPart?.SlideLayout?.CommonSlideData?.ShapeTree;
                if (layoutTree is not null)
                {
                    CollectTree(layoutTree, 0, 0, layoutOnly: true);
                }
            }

            if (tree is not null)
            {
                foreach (var videoFile in tree.Descendants().Where(IsVideoFileElement))
                {
                    TryAddVideoFromLink(
                        ReadRelationshipLink(videoFile),
                        FindTransform(videoFile));
                }
            }
        }

        private static bool IsVideoFileElement(OpenXmlElement element) =>
            string.Equals(element.LocalName, "videoFile", StringComparison.OrdinalIgnoreCase);

        private static string? ReadRelationshipLink(OpenXmlElement element)
        {
            foreach (var attr in element.GetAttributes())
            {
                if (string.Equals(attr.LocalName, "link", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(attr.Value))
                {
                    return attr.Value;
                }
            }

            return null;
        }

        private static string? TryReadBackground(SlidePart slidePart)
        {
            var solid = slidePart.Slide?
                .CommonSlideData?
                .Background?
                .Descendants<A.SolidFill>()
                .Select(f => f.RgbColorModelHex?.Val?.Value)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            if (string.IsNullOrWhiteSpace(solid))
            {
                return null;
            }

            return JsonSerializer.Serialize(new { type = "solid", color = $"#{solid}" }, JsonOptions);
        }

        private static A.Transform2D? FindTransform(OpenXmlElement blip)
        {
            var current = blip.Parent;
            while (current is not null)
            {
                if (current is P.Shape shape)
                {
                    return shape.ShapeProperties?.GetFirstChild<A.Transform2D>();
                }

                if (current is P.Picture picture)
                {
                    return picture.ShapeProperties?.GetFirstChild<A.Transform2D>();
                }

                current = current.Parent;
            }

            return null;
        }

        public void Collect(OpenXmlCompositeElement container)
        {
            CollectTree(container, 0, 0);
        }

        private void CollectTree(OpenXmlCompositeElement container, long parentX, long parentY, bool layoutOnly = false)
        {
            foreach (var child in container.ChildElements)
            {
                switch (child)
                {
                    case P.Shape shape:
                        ProcessShape(shape, parentX, parentY, layoutOnly);
                        break;
                    case P.Picture picture:
                        if (!layoutOnly)
                        {
                            ProcessPicture(picture, parentX, parentY);
                        }

                        break;
                    case P.GraphicFrame frame:
                        ProcessGraphicFrame(frame, parentX, parentY);
                        break;
                    case P.GroupShape group:
                        var (gx, gy) = GetGroupOffset(group);
                        CollectTree(group, parentX + gx, parentY + gy, layoutOnly);
                        break;
                }
            }
        }

        private static (long x, long y) GetGroupOffset(P.GroupShape group)
        {
            var xfrm = group.GroupShapeProperties?.GetFirstChild<A.TransformGroup>();
            return (xfrm?.Offset?.X?.Value ?? 0, xfrm?.Offset?.Y?.Value ?? 0);
        }

        private void ProcessShape(P.Shape shape, long parentX, long parentY, bool layoutOnly)
        {
            var text = GetText(shape.TextBody);
            if (!string.IsNullOrWhiteSpace(text))
            {
                TextBlocks.Add(text);
                var ph = shape.NonVisualShapeProperties?
                    .ApplicationNonVisualDrawingProperties?
                    .PlaceholderShape?.Type?.Value;

                if (IsTitlePlaceholderType(ph))
                {
                    Title ??= text;
                }
                else if (IsBodyPlaceholderType(ph))
                {
                    Body = string.IsNullOrWhiteSpace(Body) ? text : $"{Body}\n{text}";
                }

                AddTextElement(shape, text, parentX, parentY);
            }

            if (!layoutOnly)
            {
                TryAddPictureFromBlip(
                    shape.Descendants<A.Blip>().FirstOrDefault(),
                    shape.ShapeProperties?.GetFirstChild<A.Transform2D>(),
                    parentX,
                    parentY);
            }
        }

        private void ProcessPicture(P.Picture picture, long parentX, long parentY)
        {
            var videoLink = picture.Descendants()
                .Where(IsVideoFileElement)
                .Select(ReadRelationshipLink)
                .FirstOrDefault(link => !string.IsNullOrWhiteSpace(link));

            if (!string.IsNullOrWhiteSpace(videoLink))
            {
                TryAddVideoFromLink(
                    videoLink,
                    picture.ShapeProperties?.GetFirstChild<A.Transform2D>(),
                    parentX,
                    parentY);
            }

            TryAddPictureFromBlip(
                picture.BlipFill?.Blip,
                picture.ShapeProperties?.GetFirstChild<A.Transform2D>(),
                parentX,
                parentY);
            var desc = picture.NonVisualPictureProperties?
                .NonVisualDrawingProperties?.Description?.Value;
            if (!string.IsNullOrWhiteSpace(desc))
            {
                TextBlocks.Add(desc);
            }
        }

        private void ProcessGraphicFrame(P.GraphicFrame frame, long parentX, long parentY)
        {
            var text = string.Concat(frame.Descendants<A.Text>().Select(t => t.Text)).Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                TextBlocks.Add(text);
                var (x, y, w, h) = GetGraphicFrameBounds(frame, parentX, parentY);
                Elements.Add(BuildTextElement(text, x, y, w, h, 22, 400, "#243447"));
            }
        }

        private (int x, int y, int w, int h) GetGraphicFrameBounds(P.GraphicFrame frame, long parentX, long parentY)
        {
            var transform = frame.Transform;
            if (transform?.Offset is not null && transform.Extents is not null)
            {
                return (
                    ToPx((transform.Offset.X?.Value ?? 0) + parentX, _scaleX),
                    ToPx((transform.Offset.Y?.Value ?? 0) + parentY, _scaleY),
                    ToPx(transform.Extents.Cx?.Value ?? 400_000, _scaleX),
                    ToPx(transform.Extents.Cy?.Value ?? 300_000, _scaleY));
            }

            return (64, 140, 832, 320);
        }

        private void TryAddPictureFromBlip(A.Blip? blip, A.Transform2D? xfrm, long parentX = 0, long parentY = 0)
        {
            if (blip?.Embed?.Value is null)
            {
                return;
            }

            try
            {
                var relId = blip.Embed.Value;
                if (!_imageUrlByRelId.TryGetValue(relId, out var url))
                {
                    var imagePart = ResolveImagePart(relId);
                    if (imagePart is null)
                    {
                        return;
                    }

                    url = SaveImage(imagePart);
                    if (url is null)
                    {
                        return;
                    }

                    _imageUrlByRelId[relId] = url;
                }

                var (x, y, w, h) = GetBounds(xfrm, parentX, parentY);
                Elements.Add(new
                {
                    id = $"el-img-{Guid.NewGuid():N}"[..12],
                    type = "image",
                    x,
                    y,
                    w = Math.Max(w, 80),
                    h = Math.Max(h, 80),
                    rotation = 0,
                    z = _z++,
                    props = new { src = url, opacity = 1 }
                });
            }
            catch
            {
                // Skip broken image references.
            }
        }

        private void TryAddVideoFromLink(
            string? relId,
            A.Transform2D? xfrm,
            long parentX = 0,
            long parentY = 0)
        {
            if (string.IsNullOrWhiteSpace(relId) || !_savedVideoRelIds.Add(relId))
            {
                return;
            }

            try
            {
                var mediaPart = ResolveMediaPart(relId);
                if (mediaPart is null)
                {
                    return;
                }

                var url = SaveMediaFromPart(mediaPart);
                if (url is null)
                {
                    return;
                }

                var (x, y, w, h) = GetBounds(xfrm, parentX, parentY);
                Elements.Add(new
                {
                    id = $"el-vid-{Guid.NewGuid():N}"[..12],
                    type = "video",
                    x,
                    y,
                    w = Math.Max(w, 160),
                    h = Math.Max(h, 90),
                    rotation = 0,
                    z = _z++,
                    props = new
                    {
                        src = url,
                        autoplay = false,
                        loop = false,
                        muted = true
                    }
                });
            }
            catch
            {
                // Skip broken video references.
            }
        }

        private OpenXmlPart? ResolveMediaPart(string relId)
        {
            OpenXmlPartContainer?[] roots =
            [
                _slidePart,
                _slidePart.SlideLayoutPart,
                _presentationPart
            ];

            foreach (var root in roots)
            {
                if (root is null)
                {
                    continue;
                }

                try
                {
                    var part = root.GetPartById(relId);
                    if (part.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                    {
                        return part;
                    }
                }
                catch
                {
                    // Try next container.
                }
            }

            return null;
        }

        private string? SaveMediaFromPart(OpenXmlPart part)
        {
            var ext = part.ContentType switch
            {
                "video/mp4" => ".mp4",
                "video/quicktime" => ".mov",
                "video/x-msvideo" => ".avi",
                "video/webm" => ".webm",
                "video/x-m4v" => ".m4v",
                _ => ".mp4"
            };
            var name = $"{Guid.NewGuid():N}{ext}";
            using var input = part.GetStream();
            return _mediaStore.SaveAsync(
                input,
                name,
                part.ContentType,
                _ownerId,
                _ct).GetAwaiter().GetResult();
        }

        private ImagePart? ResolveImagePart(string relId)
        {
            OpenXmlPartContainer?[] roots =
            [
                _slidePart,
                _slidePart.SlideLayoutPart,
                _presentationPart
            ];

            foreach (var root in roots)
            {
                if (root is null)
                {
                    continue;
                }

                try
                {
                    if (root.GetPartById(relId) is ImagePart imagePart)
                    {
                        return imagePart;
                    }
                }
                catch
                {
                    // Try next container.
                }
            }

            return null;
        }

        private void AddTextElement(P.Shape shape, string text, long parentX, long parentY)
        {
            var (x, y, w, h) = GetBounds(shape.ShapeProperties?.GetFirstChild<A.Transform2D>(), parentX, parentY);
            var fontSize = GetFontSize(shape.TextBody);
            var bold = IsBold(shape.TextBody) || IsTitlePlaceholder(shape) ? 700 : 400;
            var color = ExtractTextColor(shape.TextBody);
            Elements.Add(BuildTextElement(text, x, y, w, h, fontSize, bold, color));
        }

        private object BuildTextElement(string text, int x, int y, int w, int h, int fontSize, int fontWeight, string color) =>
            new
            {
                id = $"el-txt-{Guid.NewGuid():N}"[..12],
                type = "text",
                x,
                y,
                w = Math.Max(w, 120),
                h = Math.Max(h, 40),
                rotation = 0,
                z = _z++,
                props = new
                {
                    text,
                    fontSize,
                    fontWeight,
                    color,
                    align = "left",
                    fontFamily = "Segoe UI, sans-serif"
                }
            };

        private (int x, int y, int w, int h) GetBounds(A.Transform2D? xfrm, long parentX, long parentY)
        {
            if (xfrm?.Offset is not null && xfrm.Extents is not null)
            {
                return (
                    ToPx((xfrm.Offset.X?.Value ?? 0) + parentX, _scaleX),
                    ToPx((xfrm.Offset.Y?.Value ?? 0) + parentY, _scaleY),
                    ToPx(xfrm.Extents.Cx?.Value ?? 400_000, _scaleX),
                    ToPx(xfrm.Extents.Cy?.Value ?? 300_000, _scaleY));
            }

            return (64, 140, 832, 320);
        }

        private static bool IsTitlePlaceholder(P.Shape shape)
        {
            var ph = shape.NonVisualShapeProperties?
                .ApplicationNonVisualDrawingProperties?
                .PlaceholderShape?.Type?.Value;
            return IsTitlePlaceholderType(ph);
        }

        private static bool IsTitlePlaceholderType(P.PlaceholderValues? ph) =>
            ph == P.PlaceholderValues.Title
            || ph == P.PlaceholderValues.CenteredTitle
            || ph == P.PlaceholderValues.SubTitle;

        private static bool IsBodyPlaceholderType(P.PlaceholderValues? ph) =>
            ph == P.PlaceholderValues.Body || ph == P.PlaceholderValues.Object;

        private string? SaveImage(ImagePart imagePart)
        {
            var ext = imagePart.ContentType switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/bmp" => ".bmp",
                _ => ".img"
            };
            var name = $"{Guid.NewGuid():N}{ext}";
            using var input = imagePart.GetStream();
            return _mediaStore.SaveAsync(
                input,
                name,
                imagePart.ContentType,
                _ownerId,
                _ct).GetAwaiter().GetResult();
        }

        private static int ToPx(long emu, double scale) =>
            (int)Math.Max(0, Math.Round(emu * scale));

        private static string GetText(P.TextBody? body)
        {
            if (body is null)
            {
                return "";
            }

            var lines = body.Descendants<A.Paragraph>()
                .Select(p => string.Concat(p.Descendants<A.Text>().Select(t => t.Text)).Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l));
            return string.Join("\n", lines);
        }

        private static int GetFontSize(P.TextBody? body)
        {
            var sz = body?.Descendants<A.RunProperties>().FirstOrDefault()?.FontSize?.Value;
            if (sz is null or 0)
            {
                return 24;
            }

            return Math.Clamp((int)Math.Round(sz.Value / 100.0), 12, 72);
        }

        private static bool IsBold(P.TextBody? body) =>
            body?.Descendants<A.RunProperties>().FirstOrDefault()?.Bold?.Value == true;

        private static string ExtractTextColor(P.TextBody? body)
        {
            var rgb = body?
                .Descendants<A.RunProperties>()
                .Select(r => r.GetFirstChild<A.SolidFill>()?.RgbColorModelHex?.Val?.Value)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            if (string.IsNullOrWhiteSpace(rgb))
            {
                return "#243447";
            }

            var color = $"#{rgb.TrimStart('#')}";
            return IsLightColor(color) ? "#0B1F33" : color;
        }

        private static bool IsLightColor(string hex)
        {
            if (hex.Length < 7 || !hex.StartsWith('#'))
            {
                return false;
            }

            if (!int.TryParse(hex.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)
                || !int.TryParse(hex.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
                || !int.TryParse(hex.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            {
                return false;
            }

            var luminance = (0.299 * r) + (0.587 * g) + (0.114 * b);
            return luminance > 200;
        }
    }

    private static string NormalizeSlideTitle(string? raw, int index)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return $"Diapositiva {index}";
        }

        var firstLine = raw.Trim()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? raw.Trim();

        return PresentationFieldLimits.Truncate(firstLine, PresentationFieldLimits.TitleMax);
    }
}
