using System.Text.Json;
using Cale.Modules.Presentation.Application.DTOs;
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

    public static IReadOnlyList<ImportedSlideOutline> Import(Stream stream, string uploadsDirectory)
    {
        Directory.CreateDirectory(uploadsDirectory);

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

            var collector = new SlideCollector(slidePart, uploadsDirectory, scaleX, scaleY);
            collector.Collect(tree);

            var notes = ExtractSlideNotes(slidePart);
            var title = collector.Title
                ?? collector.TextBlocks.FirstOrDefault()
                ?? $"Diapositiva {index}";
            var body = collector.Body
                ?? string.Join(
                    "\n",
                    collector.TextBlocks
                        .Where(t => !string.Equals(t, title, StringComparison.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase));

            if (collector.Elements.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
                {
                    continue;
                }

                slides.Add(new ImportedSlideOutline(
                    title.Trim(),
                    body.Trim(),
                    notes));
            }
            else
            {
                slides.Add(new ImportedSlideOutline(
                    title.Trim(),
                    body.Trim(),
                    notes,
                    PresentationTemplates.LightBackground,
                    JsonSerializer.Serialize(collector.Elements, JsonOptions)));
            }

            index++;
        }

        if (slides.Count == 0)
        {
            throw new InvalidOperationException(
                "No se pudo leer contenido del PowerPoint. Verifica que tenga texto o imágenes.");
        }

        return slides;
    }

    public static byte[] Export(PresentationDetailDto detail)
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
                AddSlide(presentationPart, slideLayoutPart, slide, ref slideId);
            }

            presentationPart.Presentation.Save();
        }

        return ms.ToArray();
    }

    private static void AddSlide(
        PresentationPart presentationPart,
        SlideLayoutPart slideLayoutPart,
        PresentationSlideDto slide,
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
        var elements = ParseElements(slide.ElementsJson);
        if (elements.Count == 0)
        {
            AppendTextShape(shapeTree, slide.Title, 64, 48, 832, 64, 32, true, ref z);
            AppendTextShape(shapeTree, ExtractPlainBody(slide.ElementsJson), 64, 140, 832, 320, 22, false, ref z);
        }
        else
        {
            foreach (var el in elements)
            {
                if (el.TryGetProperty("type", out var typeProp))
                {
                    var type = typeProp.GetString();
                    if (type == "text" && el.TryGetProperty("props", out var props))
                    {
                        var text = props.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                        var fontSize = props.TryGetProperty("fontSize", out var fs) ? fs.GetInt32() : 24;
                        var bold = props.TryGetProperty("fontWeight", out var fw) && fw.GetInt32() >= 600;
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
                    }
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

    private sealed class SlideCollector
    {
        private readonly SlidePart _slidePart;
        private readonly string _uploadsDir;
        private readonly double _scaleX;
        private readonly double _scaleY;
        private int _z = 1;

        public List<object> Elements { get; } = [];
        public List<string> TextBlocks { get; } = [];
        public string? Title { get; private set; }
        public string? Body { get; private set; }

        public SlideCollector(SlidePart slidePart, string uploadsDir, double scaleX, double scaleY)
        {
            _slidePart = slidePart;
            _uploadsDir = uploadsDir;
            _scaleX = scaleX;
            _scaleY = scaleY;
        }

        public void Collect(OpenXmlCompositeElement container)
        {
            foreach (var child in container.ChildElements)
            {
                switch (child)
                {
                    case P.Shape shape:
                        ProcessShape(shape);
                        break;
                    case P.Picture picture:
                        ProcessPicture(picture);
                        break;
                    case P.GraphicFrame frame:
                        ProcessGraphicFrame(frame);
                        break;
                    case P.GroupShape group:
                        Collect(group);
                        break;
                }
            }
        }

        private void ProcessShape(P.Shape shape)
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

                AddTextElement(shape, text);
            }

            TryAddPictureFromBlip(shape.Descendants<A.Blip>().FirstOrDefault(), shape.ShapeProperties?.GetFirstChild<A.Transform2D>());
        }

        private void ProcessPicture(P.Picture picture)
        {
            TryAddPictureFromBlip(
                picture.BlipFill?.Blip,
                picture.ShapeProperties?.GetFirstChild<A.Transform2D>());
            var desc = picture.NonVisualPictureProperties?
                .NonVisualDrawingProperties?.Description?.Value;
            if (!string.IsNullOrWhiteSpace(desc))
            {
                TextBlocks.Add(desc);
            }
        }

        private void ProcessGraphicFrame(P.GraphicFrame frame)
        {
            var text = string.Concat(frame.Descendants<A.Text>().Select(t => t.Text)).Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                TextBlocks.Add(text);
                var (x, y, w, h) = GetGraphicFrameBounds(frame);
                Elements.Add(BuildTextElement(text, x, y, w, h, 22, 400));
            }
        }

        private (int x, int y, int w, int h) GetGraphicFrameBounds(P.GraphicFrame frame)
        {
            var transform = frame.Transform;
            if (transform?.Offset is not null && transform.Extents is not null)
            {
                return (
                    ToPx(transform.Offset.X?.Value ?? 0, _scaleX),
                    ToPx(transform.Offset.Y?.Value ?? 0, _scaleY),
                    ToPx(transform.Extents.Cx?.Value ?? 400_000, _scaleX),
                    ToPx(transform.Extents.Cy?.Value ?? 300_000, _scaleY));
            }

            return (64, 140, 832, 320);
        }

        private void TryAddPictureFromBlip(A.Blip? blip, A.Transform2D? xfrm)
        {
            if (blip?.Embed?.Value is null)
            {
                return;
            }

            try
            {
                var imagePart = (ImagePart)_slidePart.GetPartById(blip.Embed.Value);
                var url = SaveImage(imagePart);
                if (url is null)
                {
                    return;
                }

                var (x, y, w, h) = GetBounds(xfrm);
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

        private void AddTextElement(P.Shape shape, string text)
        {
            var (x, y, w, h) = GetBounds(shape.ShapeProperties?.GetFirstChild<A.Transform2D>());
            var fontSize = GetFontSize(shape.TextBody);
            var bold = IsBold(shape.TextBody) || IsTitlePlaceholder(shape) ? 700 : 400;
            Elements.Add(BuildTextElement(text, x, y, w, h, fontSize, bold));
        }

        private object BuildTextElement(string text, int x, int y, int w, int h, int fontSize, int fontWeight) =>
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
                    color = "#243447",
                    align = "left",
                    fontFamily = "Segoe UI, sans-serif"
                }
            };

        private (int x, int y, int w, int h) GetBounds(A.Transform2D? xfrm)
        {
            if (xfrm?.Offset is not null && xfrm.Extents is not null)
            {
                return (
                    ToPx(xfrm.Offset.X?.Value ?? 0, _scaleX),
                    ToPx(xfrm.Offset.Y?.Value ?? 0, _scaleY),
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
            var path = Path.Combine(_uploadsDir, name);
            using var input = imagePart.GetStream();
            using var output = File.Create(path);
            input.CopyTo(output);
            return $"/uploads/presentations/{name}";
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
    }
}
