// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Infrastructure;

using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Xml;

public static class SoapTestHelper
{
    public static HttpContent CreateSoapRequest(string soapAction, string soapBody)
    {
        var soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        {soapBody}
    </soap:Body>
</soap:Envelope>";

        var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", soapAction);
        return content;
    }

    public static async Task<XmlDocument> ParseSoapResponse(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var doc = new XmlDocument();
        doc.LoadXml(responseContent);
        return doc;
    }

    public static string CreateGetConfigRequest()
    {
        return @"<GetConfig xmlns=""http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"">
            <lastChange>2023-01-01T00:00:00Z</lastChange>
            <currentTime>2024-01-01T00:00:00Z</currentTime>
        </GetConfig>";
    }

    public static string CreateSyncUpdatesRequest()
    {
        return @"<SyncUpdates xmlns=""http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"">
            <cookie>
                <Expiration>2025-01-01T00:00:00Z</Expiration>
                <EncryptedData>test-cookie-data</EncryptedData>
            </cookie>
            <parameters>
                <ExpressQuery>false</ExpressQuery>
                <InstalledNonLeafUpdateIDs/>
                <OtherCachedUpdateIDs/>
                <SkipSoftwareSync>false</SkipSoftwareSync>
                <NeedTwoGroupOutOfScopeUpdates>true</NeedTwoGroupOutOfScopeUpdates>
                <FilterAppCategoryIds/>
                <TreatAppCategoryIdsAsInstalled>false</TreatAppCategoryIdsAsInstalled>
                <AlsoPerformAUAction>false</AlsoPerformAUAction>
                <GetExtendedUpdateInfoParameters>
                    <XmlUpdateFragmentTypes>
                        <XmlUpdateFragmentType>Extended</XmlUpdateFragmentType>
                    </XmlUpdateFragmentTypes>
                    <Locales>
                        <string>en-US</string>
                    </Locales>
                </GetExtendedUpdateInfoParameters>
            </parameters>
        </SyncUpdates>";
    }

    public static string CreateGetAuthorizationCookieRequest()
    {
        return @"<GetAuthorizationCookie xmlns=""http://www.microsoft.com/SoftwareDistribution/Server/SimpleAuthWebService"">
            <username>test-user</username>
            <password>test-password</password>
        </GetAuthorizationCookie>";
    }

    public static string CreateServerSyncGetConfigRequest()
    {
        return @"<GetConfigData xmlns=""http://www.microsoft.com/SoftwareDistribution/Server/ServerSyncWebService"">
            <configAnchor></configAnchor>
        </GetConfigData>";
    }

    public static string CreateGetRevisionIdListRequest()
    {
        return @"<GetRevisionIdList xmlns=""http://www.microsoft.com/SoftwareDistribution/Server/ServerSyncWebService"">
            <filter>
                <UpdateType>Software</UpdateType>
                <LastChangeNumber>0</LastChangeNumber>
            </filter>
        </GetRevisionIdList>";
    }

    public static string CreateGetAuthConfigRequest()
    {
        return @"<GetAuthConfig xmlns=""http://www.microsoft.com/SoftwareDistribution/Server/AuthWebService"">
        </GetAuthConfig>";
    }

    public static string CreateReportEventBatchRequest()
    {
        return @"<ReportEventBatch xmlns=""http://www.microsoft.com/SoftwareDistribution/ReportingWebService"">
            <clientId>12345678-1234-1234-1234-123456789012</clientId>
            <events>
                <EventInfo>
                    <EventType>1</EventType>
                    <EventTime>2024-01-01T00:00:00Z</EventTime>
                    <EventData>Test event data</EventData>
                </EventInfo>
            </events>
        </ReportEventBatch>";
    }
}
