using System.Net;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Tests.Integration;

/// <summary>Kalendersvar og sletning af brugere gennem siderne.</summary>
public sealed class AccountDataTests(TestApp app) : IClassFixture<TestApp>
{
    [Fact]
    public async Task Rsvp_Toggles_AndRedirectsToEventMonth()
    {
        var id = await app.CreateUserAsync("rsvp");
        var eventId = app.Db(db =>
        {
            var ev = new CalendarEvent { Title = "Høstfest", StartsAt = new DateTime(2026, 9, 5), CreatedById = id, CreatedByEmail = "rsvp@tumlegaarden.dk" };
            db.Events.Add(ev);
            db.SaveChanges();
            return ev.Id;
        });
        var client = app.ClientFor("rsvp");

        var response = await client.PostFormAsync($"/Calendar?handler=Respond&id={eventId}", [new("attending", "true")], "/Calendar?m=2026-09");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Calendar?m=2026-09&e={eventId}#e-{eventId}", response.Headers.Location!.OriginalString);
        Assert.True(app.Db(db => db.EventResponses.Single(r => r.EventId == eventId).Attending));

        await client.PostFormAsync($"/Calendar?handler=Respond&id={eventId}", [new("attending", "true")], "/Calendar?m=2026-09");
        Assert.Equal(0, app.Db(db => db.EventResponses.Count(r => r.EventId == eventId)));   // samme knap igen fjerner svaret
    }

    [Fact]
    public async Task DeletingUser_RemovesTheirEmail_ButKeepsSharedContent()
    {
        await app.CreateUserAsync("slet-admin", Roles.Administrator);
        var id = await app.CreateUserAsync("slet-mig");
        app.Db(db =>
        {
            db.Quizzes.Add(new Quiz { Title = "Min quiz", CreatedById = id, CreatedByEmail = "slet-mig@tumlegaarden.dk" });
            db.Albums.Add(new Album { Title = "Mit album", CreatedById = id, CreatedByEmail = "slet-mig@tumlegaarden.dk" });
            db.GameResults.Add(new GameResult { Game = "gaade", UserId = id, UserEmail = "slet-mig@tumlegaarden.dk", Score = 60 });
            return db.SaveChanges();
        });

        var admin = app.ClientFor("slet-admin");
        var response = await admin.PostFormAsync($"/Users?handler=Delete&id={id}", ("confirm", "true"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        Assert.False(app.Db(db => db.Users.Any(u => u.Id == id)));
        Assert.Equal(UserDataEraser.DeletedUser, app.Db(db => db.Quizzes.Single(q => q.Title == "Min quiz").CreatedByEmail));
        Assert.Equal(UserDataEraser.DeletedUser, app.Db(db => db.Albums.Single(a => a.Title == "Mit album").CreatedByEmail));
        Assert.Equal(0, app.Db(db => db.GameResults.Count(r => r.UserId == id)));          // egne resultater slettes med kontoen
    }

    [Fact]
    public async Task Borler_CannotDeleteUsers()
    {
        await app.CreateUserAsync("slet-borler");
        var target = await app.CreateUserAsync("slet-maal");
        var response = await app.ClientFor("slet-borler").PostFormAsync($"/Users?handler=Delete&id={target}", ("confirm", "true"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(app.Db(db => db.Users.Any(u => u.Id == target)));
    }
}
