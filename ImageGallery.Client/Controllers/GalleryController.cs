﻿using System.Security.Claims;
using System.Text;
using ImageGallery.Client.ViewModels;
using ImageGallery.Model;
using Microsoft.AspNetCore.Mvc; 
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace ImageGallery.Client.Controllers;

[Authorize]
public class GalleryController(IHttpClientFactory httpClientFactory,
    ILogger<GalleryController> logger) : Controller
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ??
            throw new ArgumentNullException(nameof(httpClientFactory));
    private readonly ILogger<GalleryController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<IActionResult> Index()
    {
        await LogIdentityInformation();
        var httpClient = _httpClientFactory.CreateClient("APIClient");

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/images/");

        var response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        using (var responseStream = await response.Content.ReadAsStreamAsync())
        {
            var images = await JsonSerializer.DeserializeAsync<List<Image>>(responseStream);
            return View(new GalleryIndexViewModel(images ?? []));
        }
    }

    public async Task<IActionResult> EditImage(Guid id)
    {

        var httpClient = _httpClientFactory.CreateClient("APIClient");

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/images/{id}");

        var response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        using (var responseStream = await response.Content.ReadAsStreamAsync())
        {
            var deserializedImage = await JsonSerializer.DeserializeAsync<Image>(responseStream) ?? throw new Exception("Deserialized image must not be null.");
            var editImageViewModel = new EditImageViewModel()
            {
                Id = deserializedImage.Id,
                Title = deserializedImage.Title
            };

            return View(editImageViewModel);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditImage(EditImageViewModel editImageViewModel)
    {
        if (!ModelState.IsValid)
        {
            return View();
        }

        // create an ImageForUpdate instance
        var imageForUpdate = new ImageForUpdate(editImageViewModel.Title);

        // serialize it
        var serializedImageForUpdate = JsonSerializer.Serialize(imageForUpdate);

        var httpClient = _httpClientFactory.CreateClient("APIClient");

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/images/{editImageViewModel.Id}")
        {
            Content = new StringContent(
                serializedImageForUpdate,
                System.Text.Encoding.Unicode,
                "application/json")
        };

        var response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        return RedirectToAction("Index");
    }

    public async Task<IActionResult> DeleteImage(Guid id)
    {
        var httpClient = _httpClientFactory.CreateClient("APIClient");

        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/images/{id}");

        var response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        return RedirectToAction("Index");
    }

    public IActionResult AddImage()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddImage(AddImageViewModel addImageViewModel)
    {
        if (!ModelState.IsValid)
        {
            return View();
        }

        // create an ImageForCreation instance
        ImageForCreation? imageForCreation = null;

        // take the first (only) file in the Files list
        var imageFile = addImageViewModel.Files.First();

        if (imageFile.Length > 0)
        {
            using (var fileStream = imageFile.OpenReadStream())
            using (var ms = new MemoryStream())
            {
                fileStream.CopyTo(ms);
                imageForCreation = new ImageForCreation(
                    addImageViewModel.Title, ms.ToArray());
            }
        }

        // serialize it
        var serializedImageForCreation = JsonSerializer.Serialize(imageForCreation);

        var httpClient = _httpClientFactory.CreateClient("APIClient");

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/images")
        {
            Content = new StringContent(
                serializedImageForCreation,
                System.Text.Encoding.Unicode,
                "application/json")
        };

        var response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return RedirectToAction("Index");
    }

    public async Task LogIdentityInformation()
    {
        var identityToken = await HttpContext.GetTokenAsync(OpenIdConnectParameterNames.IdToken);

        var userClaimsBuilder = new StringBuilder();
        foreach (var claim in User.Claims)
        {
            userClaimsBuilder.AppendLine($"ClaimType: {claim.Type}, ClaimValue: {claim.Value}");
        }
        
        _logger.LogInformation($"Identity token: {identityToken}" + Environment.NewLine + userClaimsBuilder);
    }
}
