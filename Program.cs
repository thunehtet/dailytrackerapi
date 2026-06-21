using System.Net.Http.Headers;
using System.Threading.RateLimiting;
using daily_tracker_api.Options;
using daily_tracker_api.Services;

var builder = WebApplication.CreateBuilder(args);

var railwayOpenAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (!string.IsNullOrWhiteSpace(railwayOpenAiKey))
{
    builder.Configuration["OpenAI:ApiKey"] = railwayOpenAiKey;
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services
    .AddOptions<OpenAiOptions>()
    .Bind(builder.Configuration.GetSection(OpenAiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpClient<IExpenseInterpreter, OpenAiExpenseInterpreter>(
    (services, client) =>
    {
        var options = services.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<OpenAiOptions>>().Value;
        client.BaseAddress = new Uri("https://api.openai.com/v1/");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", options.ApiKey);
        client.Timeout = TimeSpan.FromSeconds(60);
    });
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("interpretation", context =>
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        var client = forwardedFor.Split(',', StringSplitOptions.TrimEntries)[0];
        if (string.IsNullOrWhiteSpace(client))
        {
            client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            client,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapControllers();

app.Run();

public partial class Program;
