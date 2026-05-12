using RagAMuffin.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.MapGet("/document", () =>
{
    return new
    {
        Id = 1,
        Title = "Sample Document",
        Content = "This is a sample document for testing."
    };
});

// this endpoint needs to take in a document, chunk it, and then send the chunks to the vector database
app.MapPost("/upload", (UploadRequest uploadRequest) =>
{
    return new
    {
        Message = $"Received request with name '{uploadRequest.Name}'."
    };
});


app.Run();
