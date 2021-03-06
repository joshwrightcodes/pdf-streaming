using System.IO.Compression;
using Amazon.S3;
using Amazon.S3.Transfer;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using LocalStack.Client.Extensions;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddLocalStack(builder.Configuration)
    .AddDefaultAWSOptions(builder.Configuration.GetAWSOptions())
    .AddAwsService<IAmazonS3>(); // !!!!!! different to normal .AddAWSService !!!!!!

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.MapGet("/static-pdf", async (
    HttpContext context,
    int? generate,
    CancellationToken cancellationToken) =>
{
    var staticPdfPath = Path.Combine(app.Environment.ContentRootPath, "Documents", "SamplePDF.pdf");

    var docCount = generate ?? 1;

    context.Response.ContentType = "application/octet-stream";
    context.Response.Headers.Add("Content-Disposition", "attachment; filename=\"document.zip\"");

    using var archive = new ZipArchive(context.Response.BodyWriter.AsStream(), ZipArchiveMode.Create);

    for (var i = 1; i <= docCount; i++)
    {
        var filenameWithoutExtension = Path.GetFileNameWithoutExtension(staticPdfPath);
        var filename = $"{filenameWithoutExtension}_{i:0000000000}.pdf";
        var entry = archive.CreateEntry(filename);
        await using var entryStream = entry.Open();
        await using var fileStream = File.OpenRead(staticPdfPath);
        await fileStream.CopyToAsync(entryStream, cancellationToken);
    }
});

app.MapGet("/static-s3", async(
    HttpContext context,
    int ? generate,
    [FromServices]IAmazonS3 s3Client,
    CancellationToken cancellationToken) =>
{
    const string bucket = "demo";
    const string fileKey = "a/folder/SamplePDF.pdf";

    var docCount = generate ?? 1;

    context.Response.ContentType = "application/octet-stream";
    context.Response.Headers.Add("Content-Disposition", "attachment; filename=\"document.zip\"");

    var util = new TransferUtility(s3Client);
    
    using var archive = new ZipArchive(context.Response.BodyWriter.AsStream(), ZipArchiveMode.Create);

    for (var i = 1; i <= docCount; i++)
    {
        try
        {
            var filenameWithoutExtension = Path.GetFileNameWithoutExtension(fileKey);
            var filename = $"{filenameWithoutExtension}_{i:0000000000}.pdf";
            var entry = archive.CreateEntry(filename);
            await using var entryStream = entry.Open();
            await using var fileStream = await util.OpenStreamAsync(bucket, fileKey, cancellationToken);
            await fileStream.CopyToAsync(entryStream, cancellationToken);
        }
        catch (Exception ex)
        {
            throw;
        }
    }
});

app.MapGet("/dynamic-pdf", async (
    HttpContext context,
    int? generate) =>
{
    var docCount = generate ?? 1;

    context.Response.ContentType = "application/octet-stream";
    context.Response.Headers.Add("Content-Disposition", "attachment; filename=\"document.zip\"");

    using var archive = new ZipArchive(context.Response.BodyWriter.AsStream(), ZipArchiveMode.Create);

    for (var i = 1; i <= docCount; i++)
    {
        var filename = $"dynamic-pdf_{i:0000000000}.pdf";
        var entry = archive.CreateEntry(filename);
        await using var entryStream = entry.Open();
        var pdfWriter = new PdfWriter(entryStream);
        var pdfDocument = new PdfDocument(pdfWriter);
        var document = new Document(pdfDocument);

        var header = new Paragraph($"Document {i:0000000000}")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(20);

        document.Add(header);
        document.Close();
    }
});

app.MapGet("/dynamic-pdf-single", async (
    HttpContext context,
    int? generate) =>
{
    var docCount = generate ?? 1;

    context.Response.ContentType = "application/octet-stream";
    context.Response.Headers.Add("Content-Disposition", "attachment; filename=\"document.pdf\"");

    await using var pdfWriter = new PdfWriter(context.Response.BodyWriter.AsStream());
    var pdfDocument = new PdfDocument(pdfWriter);
    var document = new Document(pdfDocument);

    for (var i = 1; i <= docCount; i++)
    {
        if (i > 1) document.Add(new AreaBreak()); // add new page

        var para = new Paragraph($"Document {i:0000000000}")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(20);

        document.Add(para);
    }

    document.Close();
});

app.Run();