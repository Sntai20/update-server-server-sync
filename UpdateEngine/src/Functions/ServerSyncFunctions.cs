// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.MicrosoftUpdate.Endpoints.ServerSync;
using System.Net;
using System.Text;

public class ServerSyncFunctions
{
    private readonly ILogger logger;
    private readonly ServerSyncWebService serverSyncService;
    private readonly AuthenticationWebService authService;
    private readonly ReportingWebService reportingService;

    public ServerSyncFunctions(
        ILoggerFactory loggerFactory,
        ServerSyncWebService serverSyncService,
        AuthenticationWebService authService,
        ReportingWebService reportingService)
    {
        this.logger = loggerFactory.CreateLogger<ServerSyncFunctions>();
        this.serverSyncService = serverSyncService;
        this.authService = authService;
        this.reportingService = reportingService;
    }

    [Function("ServerSyncWebService")]
    public async Task<HttpResponseData> ServerSyncHandler(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ServerSyncWebService/ServerSyncWebService.asmx")] HttpRequestData req)
    {
        this.logger.LogInformation("Processing server sync request");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/xml; charset=utf-8");

            if (requestBody.Contains("GetConfig"))
            {
                var result = await HandleGetServerConfigAsync(requestBody);
                await response.WriteStringAsync(result);
            }
            else if (requestBody.Contains("GetRevisionIdList"))
            {
                var result = await HandleGetRevisionIdListAsync(requestBody);
                await response.WriteStringAsync(result);
            }
            else if (requestBody.Contains("GetUpdateData"))
            {
                var result = await HandleGetUpdateDataAsync(requestBody);
                await response.WriteStringAsync(result);
            }
            else
            {
                await response.WriteStringAsync(CreateSoapFault("Unsupported server sync operation"));
            }

            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error processing server sync request");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            response.Headers.Add("Content-Type", "text/xml; charset=utf-8");
            await response.WriteStringAsync(CreateSoapFault(ex.Message));
            return response;
        }
    }

    [Function("DssAuthWebService")]
    public async Task<HttpResponseData> DssAuthHandler(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "DssAuthWebService/DssAuthWebService.asmx")] HttpRequestData req)
    {
        this.logger.LogInformation("Processing DSS auth request");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/xml; charset=utf-8");

            if (requestBody.Contains("GetAuthConfig"))
            {
                var result = await HandleGetAuthConfigAsync(requestBody);
                await response.WriteStringAsync(result);
            }
            else
            {
                await response.WriteStringAsync(CreateSoapFault("Unsupported auth operation"));
            }

            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error processing DSS auth request");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            response.Headers.Add("Content-Type", "text/xml; charset=utf-8");
            await response.WriteStringAsync(CreateSoapFault(ex.Message));
            return response;
        }
    }

    // Note: Alternative route removed due to conflict - WSUS should use the main DssAuthWebService endpoint

    [Function("ReportingWebService")]
    public async Task<HttpResponseData> ReportingHandler(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ReportingWebService/ReportingWebService.asmx")] HttpRequestData req)
    {
        this.logger.LogInformation("Processing reporting request");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/xml; charset=utf-8");

            // Handle reporting operations
            await response.WriteStringAsync(CreateSoapResponse("ReportEventBatch", "Success"));

            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error processing reporting request");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            response.Headers.Add("Content-Type", "text/xml; charset=utf-8");
            await response.WriteStringAsync(CreateSoapFault(ex.Message));
            return response;
        }
    }

    private async Task<string> HandleGetServerConfigAsync(string requestBody)
    {
        // Call the ServerSyncWebService to get configuration
        return CreateSoapResponse("GetConfig", "<ServerConfigData>...</ServerConfigData>");
    }

    private async Task<string> HandleGetRevisionIdListAsync(string requestBody)
    {
        // Call the ServerSyncWebService to get revision ID list
        return CreateSoapResponse("GetRevisionIdList", "<RevisionIdList>...</RevisionIdList>");
    }

    private async Task<string> HandleGetUpdateDataAsync(string requestBody)
    {
        // Call the ServerSyncWebService to get update data
        return CreateSoapResponse("GetUpdateData", "<UpdateData>...</UpdateData>");
    }

    private async Task<string> HandleGetAuthConfigAsync(string requestBody)
    {
        // Call the AuthenticationWebService to get auth configuration
        return CreateSoapResponse("GetAuthConfig", "<AuthConfig>...</AuthConfig>");
    }

    private string CreateSoapResponse(string operationName, string resultContent)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <{operationName}Response xmlns=""http://www.microsoft.com/SoftwareDistribution/Server/ServerSyncWebService"">
            <{operationName}Result>{resultContent}</{operationName}Result>
        </{operationName}Response>
    </soap:Body>
</soap:Envelope>";
    }

    private string CreateSoapFault(string faultString)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <soap:Fault>
            <faultcode>Server</faultcode>
            <faultstring>{faultString}</faultstring>
        </soap:Fault>
    </soap:Body>
</soap:Envelope>";
    }
}