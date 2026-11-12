namespace Tumlegaarden.Web.Data;

/// <summary>
/// En quiz i Jeopardy-stil: kategorier som kolonner og et antal rækker med stigende point.
/// Hver celle (kolonne, række) har et spørgsmål og et svar.
/// </summary>
public sealed class Quiz
{
    public const int TitleMaxLength = 80;
    public const int DescriptionMaxLength = 300;
    public const int MinColumns = 2, MaxColumns = 6, MinRows = 1, MaxRows = 6;

    public int Id { get; set; }
    public required string Title { get; set; }
    public string Description { get; set; } = "";
    public int Columns { get; set; } = 5;
    public int Rows { get; set; } = 5;
    /// <summary>Point pr. række, kommasepareret, fx "100,200,300,400,500".</summary>
    public string PointValues { get; set; } = "100,200,300,400,500";
    public required string CreatedById { get; set; }
    public required string CreatedByEmail { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }

    public List<QuizCategory> Categories { get; set; } = [];
    public List<QuizQuestion> Questions { get; set; } = [];

    public int[] Points => QuizPoints.Parse(PointValues, Rows);
}

public sealed class QuizCategory
{
    public const int NameMaxLength = 40;
    public int Id { get; set; }
    public int QuizId { get; set; }
    /// <summary>Kolonne, 0-baseret.</summary>
    public int Column { get; set; }
    public string Name { get; set; } = "";
}

public sealed class QuizQuestion
{
    public const int QuestionMaxLength = 500;
    public const int AnswerMaxLength = 200;
    public int Id { get; set; }
    public int QuizId { get; set; }
    public int Column { get; set; }
    public int Row { get; set; }
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
}

/// <summary>Et spil af en quiz: værten, holdene, point og vendte kort. Gemmes, så en quizaften kan fortsætte efter genindlæsning.</summary>
public sealed class QuizGame
{
    public const int TeamNameMaxLength = 30;
    public const int MaxTeams = 8;
    public int Id { get; set; }
    public int QuizId { get; set; }
    public Quiz? Quiz { get; set; }
    public required string HostId { get; set; }
    public required string HostEmail { get; set; }
    /// <summary>Holdnavne adskilt af linjeskift.</summary>
    public string Teams { get; set; } = "";
    /// <summary>Point pr. hold, kommasepareret i samme rækkefølge.</summary>
    public string Scores { get; set; } = "";
    /// <summary>Vendte kort som "kolonne-række" adskilt af semikolon, fx "0-0;2-3".</summary>
    public string Used { get; set; } = "";
    public bool Finished { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public List<string> TeamList => Teams.Length == 0 ? [] : [.. Teams.Split('\n')];
    public List<int> ScoreList => Scores.Length == 0 ? [] : [.. Scores.Split(',').Select(s => int.TryParse(s, out var v) ? v : 0)];
    public HashSet<string> UsedSet => Used.Length == 0 ? [] : [.. Used.Split(';')];
    public static string Cell(int column, int row) => $"{column}-{row}";
}

public static class QuizPoints
{
    /// <summary>Point pr. række. Manglende eller ugyldige værdier udfyldes med 100, 200, 300 …</summary>
    public static int[] Parse(string? values, int rows)
    {
        var parsed = (values ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var v) && v is > 0 and <= 100_000 ? v : 0).ToArray();
        return Enumerable.Range(0, rows).Select(r => r < parsed.Length && parsed[r] > 0 ? parsed[r] : (r + 1) * 100).ToArray();
    }
}
