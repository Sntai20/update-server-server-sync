// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions.Shared;

using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;

/// <summary>
/// Shared utilities for handling SOAP requests and responses across Azure Functions.
/// </summary>
public static class SoapHelpers
{
    private const string SoapContentType = "text/xml; charset=utf-8";
    private const string SoapActionHeader = "SOAPAction";

    /// <summary>
    /// Parses a SOAP request and extracts the request body.
    /// </summary>
    public static async Task<string> ParseSoapRequestAsync(HttpRequestData req)
    {
        using var reader = new StreamReader(req.Body);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Extracts the SOAP action from the request headers.
    /// </summary>
    public static string? ExtractSoapAction(HttpRequestData req)
    {
        if (req.Headers.TryGetValues(SoapActionHeader, out var values))
        {
            var soapAction = values.FirstOrDefault();
            return soapAction?.Trim('"'); // Remove quotes if present
        }

        return null;
    }

    /// <summary>
    /// Determines the SOAP method being called from the request body.
    /// </summary>
    public static string? ExtractSoapMethod(string requestBody)
    {
        try
        {
            var doc = XDocument.Parse(requestBody);
            var body = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "Body");
            var method = body?.Elements().FirstOrDefault()?.Name.LocalName;
            return method;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a properly formatted SOAP response.
    /// </summary>
    public static async Task<HttpResponseData> CreateSoapResponseAsync(
        HttpRequestData req, 
        string responseContent, 
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", SoapContentType);
        
        await response.WriteStringAsync(responseContent, Encoding.UTF8);
        return response;
    }

    /// <summary>
    /// Creates a SOAP fault response for error conditions.
    /// </summary>
    public static async Task<HttpResponseData> CreateSoapFaultAsync(
        HttpRequestData req,
        string faultCode,
        string faultString,
        ILogger logger)
    {
        logger.LogError("SOAP Fault: {FaultCode} - {FaultString}", faultCode, faultString);

        var faultXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <soap:Fault>
      <faultcode>{faultCode}</faultcode>
      <faultstring>{faultString}</faultstring>
    </soap:Fault>
  </soap:Body>
</soap:Envelope>";

        return await CreateSoapResponseAsync(req, faultXml, HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// Validates that the request is a valid SOAP request.
    /// </summary>
    public static bool IsValidSoapRequest(HttpRequestData req)
    {
        var contentType = req.Headers.GetValues("Content-Type").FirstOrDefault();
        return contentType?.Contains("text/xml") == true || 
               contentType?.Contains("application/soap+xml") == true;
    }

    /// <summary>
    /// Wraps content in a standard SOAP envelope.
    /// </summary>
    public static string WrapInSoapEnvelope(string bodyContent, string? action = null)
    {
        var envelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" 
               xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <soap:Body>
    {bodyContent}
  </soap:Body>
</soap:Envelope>";

        return envelope;
    }
}