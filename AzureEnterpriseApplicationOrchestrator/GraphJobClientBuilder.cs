// Copyright 2024 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using AzureEnterpriseApplicationOrchestrator.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureEnterpriseApplicationOrchestrator;

public class GraphJobClientBuilder<TBuilder> where TBuilder : IAzureGraphClientBuilder, new()
{
    public TBuilder _builder = new TBuilder();
    private ILogger _logger = LogHandler.GetClassLogger<GraphJobClientBuilder<TBuilder>>();

    public record CertificateStoreProperties
    {
        public string ServerUsername { get; init; }
        public string ServerPassword { get; init; }
        public string ClientCertificate { get; init; }
        public string AzureCloud { get; init; }
    }

    public record CertificateStoreV2Properties
    {
        public string ServerUsername { get; init; }
        public string ServerPassword { get; init; }
        public string ClientCertificate { get; init; }
        public string ClientCertificatePassword { get; init; }
        public string AzureCloud { get; init; }
    }

    public GraphJobClientBuilder<TBuilder> WithV1CertificateStoreDetails(CertificateStore details, string storeTypeShortName)
    {
        _logger.LogDebug($"Builder - Setting values from V1 Certificate Store Details: {JsonConvert.SerializeObject(details)}");

        CertificateStoreProperties properties = JsonConvert.DeserializeObject<CertificateStoreProperties>(details.Properties);

        _logger.LogTrace($"Builder - ClientMachine  => TenantId:            {details.ClientMachine}");
        _logger.LogTrace($"Builder - ServerUsername => ApplicationId:       {properties.ServerUsername}");
        _logger.LogTrace($"Builder - AzureCloud     => AzureCloud:          {properties.AzureCloud}");

        // The Discovery Job returns Application IDs in the format `<appid> (<friendly name>)`.
        // We split out the first part to get the Application ID.
        string normalizedAppID = details.StorePath.Split(" ")[0];

        if (storeTypeShortName == "AzureApp")
        {
            _logger.LogTrace($"Builder - StorePath      => TargetApplicationApplicationId: {details.StorePath}");
            _builder.WithTargetApplicationApplicationId(normalizedAppID);
        }
        else if (storeTypeShortName == "AzureSP")
        {
            _logger.LogTrace($"Builder - StorePath      => TargetServicePrincipalApplicationId: {details.StorePath}");
            _builder.WithTargetServicePrincipalApplicationId(normalizedAppID);
        }
        else throw new Exception($"{storeTypeShortName} is not supported by WithV1CertificateStoreDetails");

        _builder
            .WithTenantId(details.ClientMachine)
            .WithApplicationId(properties.ServerUsername)
            .WithAzureCloud(properties.AzureCloud);

        if (string.IsNullOrWhiteSpace(properties.ClientCertificate))
        {
            _logger.LogDebug("Client certificate not present - Using Client Secret authentication");
            _logger.LogTrace($"Builder - ServerPassword => ClientSecret:        {properties.ServerPassword}");
            _builder.WithClientSecret(properties.ServerPassword);
        }
        else
        {
            _logger.LogDebug("Client certificate present - Using Client Certificate authentication");
            _logger.LogTrace($"Builder - ServerPassword => ClientCertificateKeyPassword:        {properties.ServerPassword}");
            X509Certificate2 clientCert = SerializeClientCertificate(properties.ClientCertificate, properties.ServerPassword);
            _builder.WithClientCertificate(clientCert);
        }

        return this;
    }

    public GraphJobClientBuilder<TBuilder> WithV2CertificateStoreDetails(CertificateStore details)
    {
        _logger.LogDebug($"Builder - Setting values from V2 Certificate Store Details: {JsonConvert.SerializeObject(details)}");

        CertificateStoreV2Properties properties = JsonConvert.DeserializeObject<CertificateStoreV2Properties>(details.Properties);

        _logger.LogTrace($"Builder - ClientMachine             => TenantId:                     {details.ClientMachine}");
        _logger.LogTrace($"Builder - StorePath                 => TargetApplicationObjectId:    {details.StorePath}");
        _logger.LogTrace($"Builder - ServerUsername            => ApplicationId:                {properties.ServerUsername}");
        _logger.LogTrace($"Builder - AzureCloud                => AzureCloud:                   {properties.AzureCloud}");

        if (string.IsNullOrEmpty(details.ClientMachine)) throw new Exception("ClientMachine is required");
        if (string.IsNullOrEmpty(details.StorePath)) throw new Exception("StorePath is required");
        if (string.IsNullOrEmpty(properties.ServerUsername)) throw new Exception("ServerUsername is required");

        // The Discovery Job returns Object IDs in the format `<oid> (<friendly name>)`.
        // We split out the first part to get the Object ID.
        string normalizedObjectID = details.StorePath.Split(" ")[0];

        _builder
            .WithTenantId(details.ClientMachine)
            .WithApplicationId(properties.ServerUsername)
            .WithTargetObjectId(normalizedObjectID)
            .WithAzureCloud(properties.AzureCloud);

        if (!string.IsNullOrEmpty(properties.ServerPassword))
        {
            _logger.LogDebug("Client certificate not present - Using Client Secret authentication");
            _logger.LogTrace($"Builder - ServerPassword            => ClientSecret:                 {properties.ServerPassword}");
            _builder.WithClientSecret(properties.ServerPassword);
        }
        else if (!string.IsNullOrEmpty(properties.ClientCertificate))
        {
            _logger.LogDebug("Client certificate present - Using Client Certificate authentication");
            _logger.LogTrace($"Builder - ClientCertificatePassword => ClientCertificateKeyPassword: {properties.ClientCertificatePassword}");
            X509Certificate2 clientCert = SerializeClientCertificate(properties.ClientCertificate, properties.ClientCertificatePassword);
            _builder.WithClientCertificate(clientCert);
        }
        else throw new Exception("One of ClientSecret or ClientCertificate is required to authenticate with Azure Graph");

        return this;
    }


    public GraphJobClientBuilder<TBuilder> WithDiscoveryJobConfiguration(DiscoveryJobConfiguration config, string tenantId)
    {
        _logger.LogTrace($"Builder - tenantId       => TenantId: {tenantId}");
        _logger.LogTrace($"Builder - ServerUsername => ApplicationId: {config.ServerUsername}");
        _logger.LogTrace($"Builder - ServerPassword => ClientSecret: {config.ServerPassword}");

        _builder
            .WithTenantId(tenantId)
            .WithApplicationId(config.ServerUsername)
            .WithClientSecret(config.ServerPassword);

        return this;
    }

    public IAzureGraphClient Build()
    {
        return _builder.Build();
    }

    private X509Certificate2 SerializeClientCertificate(string clientCertificate, string password)
    {
        // clientCertificate is a Base64 encoded certificate that's either PEM or PKCS#12 encoded.
        // We expect that it includes a private key compatible with the dotnet standard crypto libraries.

        byte[] rawCertBytes = Convert.FromBase64String(clientCertificate);
        X509Certificate2 serializedCertificate = null;

        // Try to serialize the certificate without any special handling
        try
        {
            serializedCertificate = new X509Certificate2(rawCertBytes, password, X509KeyStorageFlags.Exportable);
            if (serializedCertificate.HasPrivateKey)
            {
                _logger.LogTrace("Successfully serialized certificate using standard X509Certificate2");
                return serializedCertificate;
            }
        }
        catch (CryptographicException e)
        {
            _logger.LogDebug($"Couldn't serialize certificate using X509Certificate2: {e.Message} - trying to serialize from PEM");
        }

        try
        {
            return SerializePemCertificateAndKey(clientCertificate, password);
        }
        catch (Exception e)
        {
            string message = $"Couldn't serialize certificate as PEM: {e.Message} - please ensure that the certificate is valid.";
            _logger.LogError(message);
            throw new CryptographicException(message);
        }
    }

    private X509Certificate2 SerializePemCertificateAndKey(string clientCertificate, string password)
    {
        _logger.LogDebug($"Attempting to serialize client certificate and private key from PEM encoding");
        ReadOnlySpan<char> utf8Cert = Encoding.UTF8.GetChars(Convert.FromBase64String(clientCertificate));

        _logger.LogTrace("Finding all PEM objects in ClientCertificate");

        ReadOnlySpan<char> certificate = new char[0];
        ReadOnlySpan<char> key = new char[0];

        int numberOfPemObjects = 0;

        while (PemEncoding.TryFind(utf8Cert, out PemFields field))
        {
            numberOfPemObjects++;
            string label = utf8Cert[field.Label].ToString();
            _logger.LogTrace($"Found PEM object with label {label} at location {field.Location}");

            if (label == "CERTIFICATE")
            {
                _logger.LogTrace($"Storing {label} as certificate for serialization");
                certificate = utf8Cert[field.Location];
            }
            else
            {
                _logger.LogTrace($"Storing {label} as private key for serialization");
                key = utf8Cert[field.Location];
            }

            // Reconstruct utf8Cert without the PEM object
            Range objectRange = field.Location;
            int start = objectRange.Start.Value;
            int end = objectRange.End.Value;
            char[] newUtf8Cert = new char[utf8Cert.Length - (end - start)];

            _logger.LogTrace($"Trimming range {field.Location} [{end - start} bytes]");
            // Copy over the slice before the start of the range
            utf8Cert.Slice(0, start).CopyTo(newUtf8Cert);
            // Copy over the slice after the end of the range
            utf8Cert.Slice(end).CopyTo(newUtf8Cert.AsSpan(start));

            utf8Cert = newUtf8Cert;
        }

        if (numberOfPemObjects != 2)
        {
            throw new CryptographicException($"Expected 2 PEM objects in ClientCertificate, found {numberOfPemObjects}");
        }

        _logger.LogDebug("Successfully extracted certificate and private key from PEM encoding - serializing certificate");
        if (string.IsNullOrEmpty(password))
        {
            return X509Certificate2.CreateFromPem(certificate, key);
        }
        else
        {
            return X509Certificate2.CreateFromEncryptedPem(certificate, key, password);
        }
    }
}
