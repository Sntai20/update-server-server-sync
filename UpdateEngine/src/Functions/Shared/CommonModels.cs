// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions.Shared;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Common models and request/response patterns used across Azure Functions.
/// </summary>
public class CommonModels
{
    /// <summary>
    /// Standard API response wrapper.
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Standard error details structure.
    /// </summary>
    public class ErrorDetails
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string? Source { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Paged response structure for list operations.
    /// </summary>
    public class PagedResponse<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public bool HasNextPage => PageNumber * PageSize < TotalCount;
        public bool HasPreviousPage => PageNumber > 1;
    }

    /// <summary>
    /// Common paging parameters.
    /// </summary>
    public class PagingParameters
    {
        [Range(1, int.MaxValue, ErrorMessage = "Page number must be greater than 0")]
        public int PageNumber { get; set; } = 1;

        [Range(1, 1000, ErrorMessage = "Page size must be between 1 and 1000")]
        public int PageSize { get; set; } = 50;

        public int Skip => (PageNumber - 1) * PageSize;
        public int Take => PageSize;
    }

    /// <summary>
    /// Common filtering parameters.
    /// </summary>
    public class FilterParameters
    {
        public string? SearchTerm { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public IEnumerable<string>? Tags { get; set; }
        public Dictionary<string, string>? CustomFilters { get; set; }
    }

    /// <summary>
    /// Operation status information.
    /// </summary>
    public class OperationStatus
    {
        public string OperationId { get; set; } = Guid.NewGuid().ToString();
        public string Status { get; set; } = "Unknown";
        public double? ProgressPercentage { get; set; }
        public string? CurrentStep { get; set; }
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration => EndTime - StartTime;
        public Dictionary<string, object>? Results { get; set; }
        public List<string>? Errors { get; set; }
        public List<string>? Warnings { get; set; }
    }
}