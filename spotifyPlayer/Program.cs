using System.Net.Http.Headers;
using System.Text.Json;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisUrl = "localhost:6379";
    return ConnectionMultiplexer.Connect(redisUrl);
});

builder.Services.AddHttpClient();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/spotify/api/now-playing", async (IConnectionMultiplexer mux, IHttpClientFactory factory) =>
{
    var db = mux.GetDatabase();
    var token = (string?)await db.StringGetAsync("spotify:access_token");

    if (string.IsNullOrEmpty(token))
        return Results.Json(new { error = "no_token", message = "Ingen Spotify-token hittades i Redis." });
    var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var response = await client.GetAsync("https://api.spotify.com/v1/me/player/currently-playing");

    if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        return Results.Json(new { playing = false });

    if (!response.IsSuccessStatusCode)
        return Results.Json(new { error = "spotify_error", status = (int)response.StatusCode });

    var content = await response.Content.ReadAsStringAsync();
    var json = JsonSerializer.Deserialize<JsonElement>(content);

    var item = json.GetProperty("item");
    var isPlaying = json.GetProperty("is_playing").GetBoolean();
    var progressMs = json.TryGetProperty("progress_ms", out var prog) ? prog.GetInt64() : 0;
    var durationMs = item.GetProperty("duration_ms").GetInt64();
    var albumUri = item.GetProperty("album").GetProperty("uri").GetString();

    return Results.Json(new
    {
        playing = isPlaying,
        progress_ms = progressMs,
        duration_ms = durationMs,
        track = new
        {
            name = item.GetProperty("name").GetString(),
            artist = item.GetProperty("artists").EnumerateArray().First().GetProperty("name").GetString(),
            album = item.GetProperty("album").GetProperty("name").GetString(),
            album_art = item.GetProperty("album").GetProperty("images").EnumerateArray().First().GetProperty("url").GetString(),
            spotify_url = albumUri,
        }
    });
});



app.MapGet("/api/now-playing", async (IConnectionMultiplexer mux, IHttpClientFactory factory) =>
{
    var db = mux.GetDatabase();
    var token = (string?)await db.StringGetAsync("spotify:access_token");

    if (string.IsNullOrEmpty(token))
        return Results.Json(new { error = "no_token", message = "Ingen Spotify-token hittades i Redis." });
    var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var response = await client.GetAsync("https://api.spotify.com/v1/me/player/currently-playing");

    if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        return Results.Json(new { playing = false });

    if (!response.IsSuccessStatusCode)
        return Results.Json(new { error = "spotify_error", status = (int)response.StatusCode });

    var content = await response.Content.ReadAsStringAsync();
    var json = JsonSerializer.Deserialize<JsonElement>(content);

    var item = json.GetProperty("item");
    var isPlaying = json.GetProperty("is_playing").GetBoolean();
    var progressMs = json.TryGetProperty("progress_ms", out var prog) ? prog.GetInt64() : 0;
    var durationMs = item.GetProperty("duration_ms").GetInt64();
    var albumUri = item.GetProperty("album").GetProperty("uri").GetString();

    return Results.Json(new
    {
        playing = isPlaying,
        progress_ms = progressMs,
        duration_ms = durationMs,
        track = new
        {
            name = item.GetProperty("name").GetString(),
            artist = item.GetProperty("artists").EnumerateArray().First().GetProperty("name").GetString(),
            album = item.GetProperty("album").GetProperty("name").GetString(),
            album_art = item.GetProperty("album").GetProperty("images").EnumerateArray().First().GetProperty("url").GetString(),
            spotify_url = albumUri,
        }
    });
});

app.Run("http://127.0.0.1:80");
