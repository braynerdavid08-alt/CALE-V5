using Cale.Modules.Catalog.Application;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace Cale.UnitTests;

public class ExamWordImportParserTests
{
    // 1×1 PNG
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [Fact]
    public void ParseLines_Reads_Numbered_Questions_And_Starred_Answer()
    {
        var parsed = ExamWordImportParser.ParseLines(
        [
            "1. ¿Color del semáforo de alto?",
            "*A. Rojo. B. Verde. C. Amarillo. D. Azul.",
            "2. Segunda",
            "A. Uno",
            "*B. Dos",
            "C. Tres",
            "D. Cuatro"
        ]);

        Assert.Equal(2, parsed.Questions.Count);
        Assert.Equal(2, parsed.MarkedCorrectCount);
        Assert.Equal("A", parsed.Questions[0].Options.Single(o => o.IsCorrect).Letter);
        Assert.Equal("B", parsed.Questions[1].Options.Single(o => o.IsCorrect).Letter);
        Assert.Null(parsed.Questions[0].Image);
    }

    [Fact]
    public void Parse_Attaches_Embedded_Image_To_Question_Stem()
    {
        using var stream = BuildDocxWithStemImage();
        var parsed = ExamWordImportParser.Parse(stream);

        Assert.Single(parsed.Questions);
        Assert.NotNull(parsed.Questions[0].Image);
        Assert.Equal("image/png", parsed.Questions[0].Image!.ContentType);
        Assert.True(parsed.Questions[0].Image.Data.Length > 0);
        Assert.Equal(1, parsed.ImagesFound);
        Assert.All(parsed.Questions[0].Options, o => Assert.Null(o.Image));
    }

    [Fact]
    public void Parse_Attaches_Image_On_Option_Line_To_That_Option()
    {
        using var stream = BuildDocxWithOptionImage();
        var parsed = ExamWordImportParser.Parse(stream);

        Assert.Single(parsed.Questions);
        Assert.Null(parsed.Questions[0].Image);
        var optB = parsed.Questions[0].Options.Single(o => o.Letter == "B");
        Assert.NotNull(optB.Image);
        Assert.Equal("image/png", optB.Image!.ContentType);
    }

    private static MemoryStream BuildDocxWithStemImage()
    {
        var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(
                   ms,
                   WordprocessingDocumentType.Document,
                   true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());
            var body = main.Document.Body!;

            body.AppendChild(ParaText("1. ¿Qué señal aparece en la imagen?"));
            body.AppendChild(ParaWithImage(main, TinyPng));
            body.AppendChild(ParaText("*A. Pare. B. Ceda. C. Prohibido. D. Velocidad."));
            main.Document.Save();
        }

        ms.Position = 0;
        return ms;
    }

    private static MemoryStream BuildDocxWithOptionImage()
    {
        var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(
                   ms,
                   WordprocessingDocumentType.Document,
                   true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());
            var body = main.Document.Body!;

            body.AppendChild(ParaText("1. Elige la figura correcta:"));
            body.AppendChild(ParaText("A. Opción A"));
            body.AppendChild(ParaWithImageAndText(main, TinyPng, "*B. Opción B con figura"));
            body.AppendChild(ParaText("C. Opción C"));
            body.AppendChild(ParaText("D. Opción D"));
            main.Document.Save();
        }

        ms.Position = 0;
        return ms;
    }

    private static Paragraph ParaText(string text) =>
        new(new Run(new Text(text)));

    private static Paragraph ParaWithImage(MainDocumentPart main, byte[] png)
    {
        var imagePart = main.AddImagePart(ImagePartType.Png);
        imagePart.FeedData(new MemoryStream(png));
        var relId = main.GetIdOfPart(imagePart);
        return new Paragraph(new Run(BuildDrawing(relId)));
    }

    private static Paragraph ParaWithImageAndText(MainDocumentPart main, byte[] png, string text)
    {
        var imagePart = main.AddImagePart(ImagePartType.Png);
        imagePart.FeedData(new MemoryStream(png));
        var relId = main.GetIdOfPart(imagePart);
        return new Paragraph(
            new Run(new Text(text)),
            new Run(BuildDrawing(relId)));
    }

    private static Drawing BuildDrawing(string relationshipId)
    {
        const long cx = 990000L;
        const long cy = 792000L;
        return new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = 1U, Name = "Picture 1" },
                new DW.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = "img.png" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = cx, Cy = cy }),
                                new A.PresetGeometry(new A.AdjustValueList())
                                {
                                    Preset = A.ShapeTypeValues.Rectangle
                                }))
                    )
                    {
                        Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture"
                    }))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U
            });
    }
}
