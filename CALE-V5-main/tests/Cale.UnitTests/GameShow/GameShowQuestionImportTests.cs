using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.GameShow.Application;
using Cale.Modules.GameShow.Application.DTOs;

namespace Cale.UnitTests.GameShow;

public sealed class GameShowQuestionImportTests
{
    [Fact]
    public void ParseJson_round_trips_create_shape()
    {
        const string json = """
            {
              "title": "Prueba",
              "teamAName": "Rojos",
              "teamBName": "Azules",
              "rounds": [
                {
                  "questionText": "¿Qué llevas en el auto?",
                  "sourceQuestionId": null,
                  "answers": [
                    { "text": "Llanta", "points": 30, "aliases": ["neumatico", "rueda"] },
                    { "text": "Gato", "points": 25, "aliases": [] },
                    { "text": "Triángulo", "points": 20, "aliases": ["triangulo"] },
                    { "text": "Botiquín", "points": 15, "aliases": [] },
                    { "text": "Extintor", "points": 10, "aliases": [] }
                  ]
                }
              ]
            }
            """;

        var body = GameShowQuestionImport.ParseJson(json);

        Assert.Equal("Prueba", body.Title);
        Assert.Equal("Rojos", body.TeamAName);
        Assert.Equal("Azules", body.TeamBName);
        Assert.Single(body.Rounds);
        Assert.Equal("¿Qué llevas en el auto?", body.Rounds[0].QuestionText);
        Assert.Equal(5, body.Rounds[0].Answers.Count);
        Assert.Equal("Llanta", body.Rounds[0].Answers[0].Text);
        Assert.Contains("neumatico", body.Rounds[0].Answers[0].Aliases!);
    }

    [Fact]
    public void ParseCsv_groups_rounds_and_aliases()
    {
        var csv = """
            ronda,pregunta,rank,respuesta,puntos,aliases
            1,"¿Señal de Pare?",1,Detenerse,40,"parar | stop"
            1,"¿Señal de Pare?",2,Ceder,25,
            1,"¿Señal de Pare?",3,Reducir,20,
            1,"¿Señal de Pare?",4,Mirar,10,
            1,"¿Señal de Pare?",5,Continuar,5,
            2,"Color del semáforo",1,Rojo,35,
            2,"Color del semáforo",2,Amarillo,30,
            2,"Color del semáforo",3,Verde,20,
            2,"Color del semáforo",4,Intermitente,10,
            2,"Color del semáforo",5,Apagado,5,
            """;

        var body = GameShowQuestionImport.ParseCsv(csv);

        Assert.Equal(2, body.Rounds.Count);
        Assert.Equal("¿Señal de Pare?", body.Rounds[0].QuestionText);
        Assert.Equal("Detenerse", body.Rounds[0].Answers[0].Text);
        Assert.Equal(40, body.Rounds[0].Answers[0].Points);
        Assert.Equal(2, body.Rounds[0].Answers[0].Aliases!.Count);
        Assert.Equal("Color del semáforo", body.Rounds[1].QuestionText);
        Assert.Equal(5, body.Rounds[1].Answers.Count);
    }

    [Fact]
    public void Parse_rejects_empty()
    {
        var ex = Assert.Throws<DomainException>(() => GameShowQuestionImport.Parse("  ", "x.json"));
        Assert.Equal("empty_import", ex.ErrorCode);
    }

    [Fact]
    public void Parse_detects_json_by_content()
    {
        var body = GameShowQuestionImport.Parse(
            """{"title":"T","teamAName":"A","teamBName":"B","rounds":[{"questionText":"Pregunta larga ok","answers":[{"text":"1","points":5},{"text":"2","points":4},{"text":"3","points":3},{"text":"4","points":2},{"text":"5","points":1}]}]}""",
            "pack.txt");

        Assert.Equal("T", body.Title);
        Assert.Single(body.Rounds);
    }
}
