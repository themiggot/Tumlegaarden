using Tumlegaarden.Web.Data;

namespace Tumlegaarden.Tests;

public class QuizTests
{
    [Theory]
    [InlineData("100,200,300,400,500", 5, new[] { 100, 200, 300, 400, 500 })]
    [InlineData("200, 400", 2, new[] { 200, 400 })]
    [InlineData("200;400 600", 3, new[] { 200, 400, 600 })]
    [InlineData("10,20", 4, new[] { 10, 20, 300, 400 })]       // manglende rækker udfyldes
    [InlineData("abc,-5,0,999999", 4, new[] { 100, 200, 300, 400 })]   // ugyldige værdier erstattes
    [InlineData(null, 2, new[] { 100, 200 })]
    [InlineData("100,200,300", 1, new[] { 100 })]
    public void Points_AreParsedAndPadded(string? values, int rows, int[] expected) =>
        Assert.Equal(expected, QuizPoints.Parse(values, rows));

    [Fact]
    public void Game_ParsesTeamsScoresAndUsedCards()
    {
        var game = new QuizGame { HostId = "x", HostEmail = "x@tumlegaarden.dk", Teams = "Røde\nBlå", Scores = "-200,400", Used = "0-1;2-0" };
        Assert.Equal(["Røde", "Blå"], game.TeamList);
        Assert.Equal([-200, 400], game.ScoreList);
        Assert.True(game.UsedSet.SetEquals(["0-1", "2-0"]));
        Assert.Equal("3-4", QuizGame.Cell(3, 4));
        Assert.Empty(new QuizGame { HostId = "x", HostEmail = "x" }.TeamList);
    }
}
