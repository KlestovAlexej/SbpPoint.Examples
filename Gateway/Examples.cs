using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NUnit.Framework;
using ShtrihM.SbpPoint.Gateway.Api.Clients;
using ShtrihM.Wattle3.Json.Extensions;
using RestSharp;
using ShtrihM.SbpPoint.Processing.Api.Common;
using ShtrihM.SbpPoint.Processing.Api.Common.Dtos.Enterprises.Payments.AutomationDynamicQrs;
using ShtrihM.Wattle3.Common.Exceptions;

namespace ShtrihM.SbpPoint.Examples.Gateway;

/// <summary>
/// Примеры использования API шлюза сервера обеспечения взаимодействия с системой быстрых платежей.
/// </summary>
[TestFixture]
public class Examples
{
    /// <summary>
    /// Базовый URL API шлюза сервера обеспечения взаимодействия с системой быстрых платежей.
    /// </summary>
    private static readonly string BaseAddress = "https://localhost:9904/";

    /// <summary>
    /// Ключ API.
    /// </summary>
    private static readonly string ApiKey = @"7r8TCjiXMq5XYoqdOMl10jGGskj0QUNbJFkdYW+3IPQlMhUDD1harHUf0NRIGqJB";

    /// <summary>
    /// Публичный корневой сертификат сервера для HTTPS.
    /// </summary>
    private readonly X509Certificate2 m_rootServerCertificateHttps;

    /// <summary>
    /// Настроенный клиент HTTPS.
    /// </summary>
    private readonly RestClient m_restClient;

    public Examples()
    {
        var rootServerCertificateHttpsBytes = File.ReadAllBytes(@"root.sbppoint.gateway.server.cer");
        m_rootServerCertificateHttps = new X509Certificate2(rootServerCertificateHttpsBytes);

        var handler = GatewayClient.NewHttpClientHandler(m_rootServerCertificateHttps, true);
        m_restClient =
            new RestClient(
                useClientFactory: true,
                configureRestClient:
                options =>
                {
                    options.BaseUrl = new Uri(BaseAddress);
                    options.ConfigureMessageHandler = _ => handler;
                },
                configureSerialization:
                config =>
                {
                    GatewayClient.UpdateSerializerConfig(config);
                });
    }

    /// <summary>
    /// Создание <see cref="RestClient"/> руками.
    /// </summary>
    [Test]
    public async Task Example_Manual_RestClient()
    {
        var customTrustStore =
            new X509Certificate2Collection
            {
                m_rootServerCertificateHttps
            };
        var customChainPolicy =
            new X509ChainPolicy
            {

                RevocationMode = X509RevocationMode.NoCheck,
                VerificationFlags = X509VerificationFlags.IgnoreWrongUsage,
                TrustMode = X509ChainTrustMode.CustomRootTrust,
            };
        customChainPolicy.CustomTrustStore.AddRange(customTrustStore);

        var handler =
            new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    (_, certificate, chain, _) =>
                    {
                        var customChain =
                            new X509Chain
                            {
                                ChainPolicy = customChainPolicy
                            };
                        customChain.Build(certificate);

                        foreach (var element in customChain.ChainElements)
                        {
                            if (element.Certificate.Thumbprint == m_rootServerCertificateHttps!.Thumbprint)
                            {
                                return true;
                            }
                        }

                        foreach (var element in chain.ChainElements)
                        {
                            if (element.Certificate.Thumbprint == m_rootServerCertificateHttps.Thumbprint)
                            {
                                return true;
                            }
                        }

                        return false;
                    },
            };

        var restClient =
            new RestClient(
                useClientFactory: true,
                configureRestClient:
                options =>
                {
                    options.BaseUrl = new Uri(BaseAddress);
                    options.ConfigureMessageHandler = _ => handler;
                },
                configureSerialization:
                config =>
                {
                    GatewayClient.UpdateSerializerConfig(config);
                });

        using var client = new GatewayClient(restClient, true);
        var description = await client.GetDescriptionAsync();

        Assert.IsNotNull(description);
        Console.WriteLine(description.ToJsonText(true));
    }

    /// <summary>
    /// Получить описание сервера.
    /// </summary>
    [Test]
    public async Task Example_GetDescriptionAsync()
    {
        using var client = new GatewayClient(m_restClient);
        var description = await client.GetDescriptionAsync();

        Assert.IsNotNull(description);
        Console.WriteLine(description.ToJsonText(true));
    }

    /// <summary>
    /// Создание программируемого динамического QR-кода.
    /// </summary>
    [Test]
    public void Example_PaymentsAutomationDynamicQrsCreateAsync()
    {
        using var client = new GatewayClient(m_restClient);
        var workflowException =
            Assert.ThrowsAsync<WorkflowException>(
                async () => await client.PaymentsAutomationDynamicQrsCreateAsync(
                    ApiKey,
                    new AutomationDynamicQrCreate
                    {
                        Amount = 1000,
                        AutoCancelMinutes = 5,
                        Purpose = "Тест (10 рублей)"
                    }));
        Assert.AreEqual(WorkflowErrorCodes.AccessDenied, workflowException!.Code);
        Assert.AreEqual("Истёк срок годности ключа API", workflowException.Details, workflowException.Details);
    }
}
