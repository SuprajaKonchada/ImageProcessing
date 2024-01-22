using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

public class HomeController : Controller
{
    private readonly IWebHostEnvironment _hostingEnvironment;

    public HomeController(IWebHostEnvironment hostingEnvironment)
    {
        _hostingEnvironment = hostingEnvironment;
    }

    public IActionResult Index()
    {
        return View();
    }

    private async Task ProcessImageAsync(string filePath, float brightnessFactor, float contrastFactor, string filter, string suffix)
    {
        try
        {
            var processedFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "images", $"{suffix}_{Path.GetFileName(filePath)}");

            using (var image = await Image.LoadAsync(filePath))
            {
                image.Mutate(x => x
                    .Brightness(brightnessFactor)
                    .Contrast(contrastFactor));

                ApplyFilter(image, filter);

                await image.SaveAsync(processedFilePath);
            }
        }
        catch (Exception ex)
        {
            
            throw;
        }
    }

    private void ApplyFilter(Image image, string filter)
    {
        if (filter != null)
        {
            switch (filter.ToLower())
            {
                case "grayscale":
                    image.Mutate(x => x.Grayscale());
                    break;
                case "sepia":
                    image.Mutate(x => x.Sepia());
                    break;
                case "blur":
                    image.Mutate(x => x.GaussianBlur(5)); 
                    break;
                case "sharpen":
                    image.Mutate(x => x.GaussianSharpen());
                    break;

            }
        }
    }

    [HttpPost]
    public async Task<IActionResult> UploadImage(ImageModel model, float brightness, float contrast, string filter, float resizeFactor)
    {
        try
        {
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                // Check file extension
                var allowedExtensions = new[] { ".jpg", ".jpeg" };
                var fileExtension = Path.GetExtension(model.ImageFile.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    ViewBag.ErrorMessage = "Only JPG files are allowed.";
                    return View("Index");
                }

                var uploadPath = Path.Combine(_hostingEnvironment.WebRootPath, "images");

                if (!Directory.Exists(uploadPath))
                {
                    Directory.CreateDirectory(uploadPath);
                }

                var uniqueFileName = $"{Guid.NewGuid().ToString()}_{model.ImageFile.FileName}";
                var filePath = Path.Combine(uploadPath, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(fileStream);
                }

                // Asynchronously process and save images
                var brightnessContrastTask = ProcessImageAsync(filePath, brightness, contrast, null, "processed_brightness_contrast");
                var filterTask = ProcessImageAsync(filePath, 1.0f, 1.0f, filter, "processed_filter");
                var resizeTask = ResizeImageAsync(filePath, resizeFactor, "resized");

                await Task.WhenAll(brightnessContrastTask, filterTask, resizeTask);

                // Set the image URLs to be used in the redirect
                var originalUrl = $"/images/{Path.GetFileName(filePath)}";
                var processedBrightnessContrastUrl = $"/images/processed_brightness_contrast_{Path.GetFileName(filePath)}";
                var processedFilterUrl = $"/images/processed_filter_{Path.GetFileName(filePath)}";
                var resizedUrl = $"/images/resized_{Path.GetFileName(filePath)}";

                // Redirect to the DisplayImages action
                return RedirectToAction("DisplayImages", new
                {
                    originalUrl,
                    processedBrightnessContrastUrl,
                    processedFilterUrl,
                    resizedUrl,
                    downloadOriginalUrl = Url.Action("DownloadImage", "Home", new { imageUrl = originalUrl }),
                    downloadProcessedBrightnessContrastUrl = Url.Action("DownloadImage", "Home", new { imageUrl = processedBrightnessContrastUrl }),
                    downloadProcessedFilterUrl = Url.Action("DownloadImage", "Home", new { imageUrl = processedFilterUrl }),
                    downloadResizedUrl = Url.Action("DownloadImage", "Home", new { imageUrl = resizedUrl })
                });
            }
        }
        catch (FileNotFoundException ex)
        {
            ViewBag.ErrorMessage = $"File not found: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            ViewBag.ErrorMessage = $"Unauthorized access to the file: {ex.Message}";
        }
        catch (Exception ex)
        {
            ViewBag.ErrorMessage = $"An error occurred while processing the image: {ex.Message}";
        }

        return View("Index");
    }



    private async Task ResizeImageAsync(string filePath, float scaleFactor, string suffix)
    {
        try
        {
            var resizedFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "images", $"{suffix}_{Path.GetFileName(filePath)}");

            using (var image = await Image.LoadAsync(filePath))
            {
                var newWidth = (int)(image.Width * scaleFactor);
                var newHeight = (int)(image.Height * scaleFactor);

                image.Mutate(x => x.Resize(newWidth, newHeight));

                await image.SaveAsync(resizedFilePath);
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }
    public IActionResult DisplayImages(string originalUrl, string processedBrightnessContrastUrl, string processedFilterUrl, string resizedUrl)
    {
        try
        {
            // Check if URLs are valid
            if (string.IsNullOrWhiteSpace(originalUrl) || string.IsNullOrWhiteSpace(processedBrightnessContrastUrl) || string.IsNullOrWhiteSpace(processedFilterUrl) || string.IsNullOrWhiteSpace(resizedUrl))
            {
                ViewBag.ErrorMessage = "Invalid image URLs. Please upload an image and try again.";
                return View("Index");
            }

            ViewBag.OriginalImageUrl = originalUrl;
            ViewBag.ProcessedBrightnessContrastImageUrl = processedBrightnessContrastUrl;
            ViewBag.ProcessedFilterImageUrl = processedFilterUrl;
            ViewBag.ResizedImageUrl = resizedUrl;

            return View("DisplayImages");
        }
        catch (Exception ex)
        {
            ViewBag.ErrorMessage = $"An error occurred while displaying the images: {ex.Message}";
            return View("Index");
        }
    }

    [HttpGet]
    public IActionResult DownloadImage(string imageUrl)
    {
        var filePath = Path.Combine(_hostingEnvironment.WebRootPath, imageUrl.TrimStart('/'));

        if (System.IO.File.Exists(filePath))
        {
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "image/jpeg", "Image.jpg");
        }

        return NotFound();
    }

}