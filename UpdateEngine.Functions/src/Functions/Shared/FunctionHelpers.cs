// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions.Shared;

using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;

/// <summary>
/// Shared utilities for common Azure Functions patterns and operations.
/// </summary>
public static class FunctionHelpers
{
    private const string JsonContentType = "application/json; charset=utf-8";
    private const string TextContentType = "text/plain; charset=utf-8";

    /// <summary>
    /// Executes an operation with standardized error handling and logging.
    /// </summary>
    public static async Task<HttpResponseData> ExecuteWithErrorHandlingAsync<T>(
        HttpRequestData req,
        ILogger logger,
        Func<Task<T>> operation,
        JsonSerializerOptions? jsonOptions = null)
    {
        try
        {
            var result = await operation();
            return await CreateJsonResponseAsync(req, result, jsonOptions);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid request parameters: {Message}", ex.Message);
            return await CreateErrorResponseAsync(req, $"Invalid request: {ex.Message}", HttpStatusCode.BadRequest);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Unauthorized access attempt: {Message}", ex.Message);
            return await CreateErrorResponseAsync(req, "Unauthorized access", HttpStatusCode.Unauthorized);
        }
        catch (NotSupportedException ex)
        {
            logger.LogWarning(ex, "Unsupported operation: {Message}", ex.Message);
            return await CreateErrorResponseAsync(req, $"Operation not supported: {ex.Message}", HttpStatusCode.NotImplemented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in function execution: {Message}", ex.Message);
            return await CreateErrorResponseAsync(req, "Internal server error", HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Creates a JSON response with the specified data.
    /// </summary>
    public static async Task<HttpResponseData> CreateJsonResponseAsync<T>(
        HttpRequestData req, 
        T data, 
        JsonSerializerOptions? jsonOptions = null,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var response = req.CreateResponse(statusCode);
        
        // Set Content-Type header (overwrites if already exists)
        response.Headers.Remove("Content-Type");
        response.Headers.Add("Content-Type", JsonContentType);
        
        var json = JsonSerializer.Serialize(data, jsonOptions ?? GetDefaultJsonOptions());
        await response.WriteStringAsync(json, Encoding.UTF8);
        
        return response;
    }

    /// <summary>
    /// Creates a standardized error response.
    /// </summary>
    public static async Task<HttpResponseData> CreateErrorResponseAsync(
        HttpRequestData req,
        string message,
        HttpStatusCode statusCode,
        string? details = null)
    {
        var errorData = new
        {
            Error = message,
            StatusCode = (int)statusCode,
            Details = details,
            Timestamp = DateTime.UtcNow
        };

        return await CreateJsonResponseAsync(req, errorData, null, statusCode);
    }

    /// <summary>
    /// Creates a plain text response.
    /// </summary>
    public static async Task<HttpResponseData> CreateTextResponseAsync(
        HttpRequestData req,
        string content,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var response = req.CreateResponse(statusCode);
        
        // Set Content-Type header (overwrites if already exists)
        response.Headers.Remove("Content-Type");
        response.Headers.Add("Content-Type", TextContentType);
        
        await response.WriteStringAsync(content, Encoding.UTF8);
        return response;
    }

    /// <summary>
    /// Parses JSON request body to the specified type.
    /// </summary>
    public static async Task<T?> ParseJsonRequestAsync<T>(
        HttpRequestData req,
        JsonSerializerOptions? jsonOptions = null) where T : class
    {
        try
        {
            var body = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(body))
                return null;

            return JsonSerializer.Deserialize<T>(body, jsonOptions ?? GetDefaultJsonOptions());
        }
        catch (JsonException)
        {
            throw new ArgumentException("Invalid JSON format in request body");
        }
    }

    /// <summary>
    /// Validates that the request has the expected content type.
    /// </summary>
    public static bool ValidateContentType(HttpRequestData req, string expectedContentType)
    {
        var contentType = req.Headers.GetValues("Content-Type").FirstOrDefault();
        return contentType?.StartsWith(expectedContentType, StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Extracts query parameter values from the request.
    /// </summary>
    public static string? GetQueryParameter(HttpRequestData req, string parameterName)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        return query[parameterName];
    }

    /// <summary>
    /// Gets the default JSON serializer options used across functions.
    /// </summary>
    public static JsonSerializerOptions GetDefaultJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Creates a success response for operations that don't return data.
    /// </summary>
    public static async Task<HttpResponseData> CreateSuccessResponseAsync(
        HttpRequestData req,
        string? message = null)
    {
        var result = new
        {
            Success = true,
            Message = message ?? "Operation completed successfully",
            Timestamp = DateTime.UtcNow
        };

        return await CreateJsonResponseAsync(req, result);
    }

    /// <summary>
    /// Validates that required parameters are present.
    /// </summary>
    public static void ValidateRequiredParameters(params (string name, object? value)[] parameters)
    {
        var missing = parameters
            .Where(p => p.value == null || (p.value is string s && string.IsNullOrEmpty(s)))
            .Select(p => p.name)
            .ToList();

        if (missing.Any())
        {
            throw new ArgumentException($"Missing required parameters: {string.Join(", ", missing)}");
        }
    }
}
